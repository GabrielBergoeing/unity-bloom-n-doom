# Arquitectura de conexión online

Documento central de todo lo construido para reemplazar el flujo de "escribir IP y
puerto a mano" por un código de sala compartible, con múltiples niveles de fallback
para que el host (un jugador, no un servidor dedicado) sea alcanzable por amigos en
otras redes sin que nadie tenga que configurar su router manualmente. Los documentos
específicos de cada pieza (`UPnP-Verification.md`, `Windows-Firewall-Notes.md`,
`Tools/SignalingServer/README.md`) profundizan cada uno de los pasos de abajo; esto es
el mapa general.

## El flujo, de punta a punta

1. **El host** genera un código de sala (`JoinCode.Encode`) que contiene su IP LAN, su
   IP pública y el puerto — 18 caracteres, Base32 con checksum. Al empezar a hostear
   (`SteamLobby.HostDirect`), en paralelo:
   - `WindowsFirewallHelper` abre el puerto en el firewall de Windows (best-effort, un
     único permiso UAC la primera vez).
   - `UpnpPortMapper` intenta abrir el puerto en el router automáticamente (best-effort,
     no todos los routers lo soportan).
   - `HolePunchClient` se registra en el servidor de señalización externo con ese mismo
     código como ID de sala (best-effort, requiere tener uno desplegado y configurado).
2. **El jugador que se une** pega el código; `JoinCodeConnectionProvider` lo decodifica
   y `SteamLobby.JoinWithFallback` prueba, en orden, hasta que uno funcione:
   1. **IP LAN** — instantáneo, no depende de NAT. Cubre misma red / LAN party.
   2. **IP pública directa** — funciona si el paso 1 (firewall) y/o UPnP/forwarding
      manual ya abrieron el camino.
   3. **Hole punching vía servidor de señalización** — último recurso: ambos lados se
      descubren mutuamente y perforan su NAT para conectar directo, sin que el router
      necesite configuración manual. Cubre la mayoría de los routers hogareños (NAT
      "cone"); no cubre NAT simétrica ni CGNAT (ver limitaciones abajo).

## Mapa de archivos

| Archivo | Rol |
|---|---|
| `JoinCode.cs` | Codifica/decodifica IP LAN + IP pública + puerto en un código de 18 caracteres. |
| `IConnectionProvider.cs` / `JoinCodeConnectionProvider.cs` | Resuelve el input del usuario a una `ConnectionInfo` (dirección primaria + de respaldo). Pensado para que un futuro `GameLiftConnectionProvider` se enchufe sin tocar la UI. |
| `GameLiftConnectionProvider.cs` | Tercera vía de conexión, vía AWS GameLift - ver sección GameLift abajo. |
| `NetworkLaunchRequest.cs` | Puente estático entre la UI (`UI_OnlineDirectMenu`) y `SteamLobby` al cambiar de escena. |
| `SteamLobby.cs` | Orquesta todo: aplica el launch request, prefiere `PersonalizedTransport` sobre `KcpTransport`, corre los 3 niveles de fallback al unirse, dispara UPnP/firewall/hole-punch al hostear. |
| `PersonalizedTransport.cs` | Transporte UDP propio (no kcp2k). Soporta fijar el puerto local del cliente y reenviar paquetes crudos de señalización por el socket del servidor - ninguna de las dos cosas es posible con `KcpTransport` (ver limitación abajo). |
| `NetworkAddressUtil.cs` | Helper compartido para la IP LAN local. |
| `UpnpPortMapper.cs` | Cliente UPnP IGD (SSDP + SOAP) para abrir el puerto en el router automáticamente. |
| `WindowsFirewallHelper.cs` | Abre el puerto en el firewall de Windows automáticamente (un permiso UAC la primera vez). |
| `HolePunchClient.cs` | Habla con el servidor de señalización (`Tools/SignalingServer`) para el hole punching. |
| `UI_OnlineDirectMenu.cs` | UI: genera/muestra el código del host, autodetecta IP LAN/pública, campo único para pegar el código al unirse. |

## Limitaciones conocidas (no son bugs, son límites reales)

- **NAT simétrica / CGNAT**: ni el hole punching ni el forwarding manual funcionan acá
  — no hay solución de software, hace falta un relay o servidor dedicado (ver GameLift
  más abajo). Detalle y cómo diagnosticarlo en `UPnP-Verification.md`.
- **KCP no soporta hole punching**: `KcpTransport` (kcp2k, librería de terceros
  vendorizada) no permite fijar el puerto local del cliente antes de conectar, y esa
  librería no expone ninguna forma de hacerlo sin parchear su código fuente (posible,
  pero es un fork de código de terceros que habría que reaplicar en cada actualización
  de Mirror). Con KCP como transporte activo, los niveles LAN e IP pública directa
  siguen funcionando igual; el nivel de hole punching no. Por eso `SteamLobby` prefiere
  `PersonalizedTransport` por defecto (ver `ApplyTransportOverride`).
- **El servidor de señalización depende de que esté corriendo**: si se aloja en una PC
  de alguien (la opción rápida de prueba) en vez de un servidor siempre activo (Oracle
  Cloud Free Tier, un VPS, etc.), el nivel de hole punching deja de estar disponible
  para *cualquiera* mientras esa PC/servidor no esté encendido — no es específico de
  quién esté jugando. Ver `Tools/SignalingServer/README.md` para las opciones de
  despliegue permanente.

## GameLift

Tercera vía de conexión, alternativa al P2P de arriba: en vez de que un jugador hostee
desde su propia PC, un servidor dedicado (build `UNITY_SERVER`) corre como compute de AWS
GameLift. Soporta **dos modelos de despliegue**, autodetectados en runtime por
`GameLiftServerManager.TryInitSdk()` (ver esa función para el detalle):

- **Fleet administrada (managed EC2)**: AWS levanta y baja instancias EC2 solas.
  `InitSDK()` sin parámetros - el propio agente de GameLift en la instancia inyecta las
  variables `GAMELIFT_SDK_*` antes de lanzar el proceso. **Requiere Windows Server 2022**
  como sistema operativo del build/fleet - probado y confirmado. Windows Server 2016 y
  Amazon Linux 2023 **no funcionan**: en AL2023 el agente nunca llega a inyectar esas
  variables (bug de AWS, confirmado corriendo el mismo build local con las variables
  puestas a mano - ver issue
  https://github.com/amazon-gamelift/amazon-gamelift-plugin-unity/issues/283); en Windows
  Server 2016 el proceso crashea al arrancar con `STATUS_ENTRYPOINT_NOT_FOUND`
  (`0xC0000139`) - Unity 6 necesita APIs de Windows más nuevas que las que trae esa versión
  del SO. Windows Server 2022 evita ambos problemas.
- **Anywhere**: el compute es una máquina propia (ver `Tools/GameLiftLauncher`).
  `InitSDK(ServerParameters)` con websocket URL/host id/fleet id/auth token leídos de
  variables de entorno - el auth token expira (~15min), `Tools/GameLiftLauncher` pide uno
  fresco antes de cada arranque.

GameLift devuelve un `{ip/dnsName, port}` de sesión más un `PlayerSessionId`, mismo "shape"
que ya produce `ConnectionInfo` (`address`, `port`, `sessionToken`). El `sessionToken` se
reenvía automáticamente a `GameLiftPlayerAuthenticator.clientPlayerSessionId` desde
`SteamLobby.ApplyRuntimeLaunchRequest`, sin tocar la UI ni `SteamLobby`.

Piezas:

| Archivo | Rol |
|---|---|
| `UI_GameLiftMenu.cs` | UI: botón "Conectar", pide una sesión al broker y navega a `CharacterSelectorOnline`. `brokerUrl` vacío = no-op seguro hasta desplegar el broker. |
| `GameLiftConnectionProvider.cs` | POST `{brokerUrl}/request-session`, devuelve `ConnectionInfo` (timeout configurable, 35s por defecto - el broker puede tardar hasta 30s creando una sesión nueva). |
| `Tools/GameLiftBroker` | Proceso .NET separado, el único que tiene credenciales AWS; habla con `AmazonGameLiftClient` (crear/buscar `GameSession`, crear `PlayerSession`). Sirve para ambos modelos - solo cambia a qué fleet apunta (`GAMELIFT_FLEET_ID`/`GAMELIFT_LOCATION`). Ver su propio README para el despliegue. |
| `GameLiftServerManager.cs` (`#if UNITY_SERVER`) | Corre solo en el build de servidor dedicado. `TryInitSdk()` autodetecta administrada vs Anywhere según si las variables de entorno de Anywhere están presentes. Arranca Mirror al recibir `OnStartGameSession`. |
| `GameLiftPlayerAuthenticator.cs` | `NetworkAuthenticator` en el `Network Manager` prefab: valida el `PlayerSessionId` contra GameLift en servidores `UNITY_SERVER`; en cualquier otro build (host P2P normal) acepta automáticamente, sin cambiar el comportamiento existente. |
| `Tools/GameLiftLauncher` | Solo para el modelo Anywhere: pide un auth token fresco (`aws gamelift get-compute-auth-token`) y lanza el build del servidor con las variables de entorno correctas. |

Requisitos de infraestructura AWS (fuera de este repo, hay que provisionarlos/mantenerlos
aparte): o bien una fleet administrada (Windows Server 2022, build subido con
`--server-sdk-version 5.4.0`) o una fleet Anywhere con un compute registrado; en ambos
casos, el broker corriendo en algún lugar alcanzable 24/7 por los jugadores (puerto
propio, no el de hole punching), y las credenciales AWS del broker con permisos mínimos
de GameLift (`CreateGameSession`/`DescribeGameSessions`/`CreatePlayerSession`).

Notas prácticas de la ruta de lanzamiento (`LaunchPath` en `RuntimeConfiguration` de la
fleet administrada): no puede contener `&` (regex de validación de AWS:
`[A-Za-z0-9_:.+\/\\\- ]+`) y en Windows AWS instala el build en `C:\game\` (case-sensitive
en su propia validación aunque Windows no lo sea) - construir el path completo como
`C:\game\<nombre-del-exe>.exe` exactamente.

### Bugs encontrados en producción (fleet administrada, ya arreglados)

Esta fleet corre un único proceso de servidor de larga duración que hostea partidas
seguidas (los jugadores vuelven al lobby y arrancan otra, en vez de que GameLift levante
un proceso nuevo por partida) - varios de estos bugs solo aparecen bajo ese modelo, no en
una partida P2P de una sola vez.

- **La sala queda "llena" para siempre después de la primera partida** (`GameSessionFullException`,
  el broker respondía 500 crudo). Causa: nada llamaba nunca a `RemovePlayerSession` de
  GameLift cuando un jugador se desconectaba, así que el conteo de jugadores de la
  `GameSession` solo subía y nunca bajaba - a las 4 conexiones (`GAMELIFT_MAX_PLAYERS`),
  toda partida futura fallaba al pedir sesión. Arreglado en dos capas:
  - `GameLiftServerManager.OnConnectionDisconnected(connectionId)` busca el
    `playerSessionId` asociado a esa conexión y llama `RemovePlayerSession` - se invoca
    desde `OnlineNetworkManager.OnServerDisconnect`, que corre para *cualquier*
    desconexión (voluntaria o por timeout), no solo cuando alguien aprieta "salir".
  - `Tools/GameLiftBroker/Program.cs` ahora atrapa `GameSessionFullException` en
    `CreatePlayerSessionAsync` y devuelve `{success:false, error:"..."}` con 200 en vez de
    dejar que el 500 sin manejar llegue crudo al cliente - solo cambia el mensaje, no
    reemplaza el fix de arriba.
  - `RemovePlayerSession` **solo se puede llamar desde el propio proceso del servidor**
    (server SDK) - no existe como comando de la CLI de AWS (`aws gamelift
    remove-player-session` no existe), así que no hay forma de liberar una sesión
    atascada desde afuera; si un build viejo sin este fix se queda con la sala llena, la
    única forma de desatascarla es reiniciar la instancia (escalar a 0 y de vuelta a 1),
    lo que mata el proceso viejo y la `GameSession` junto con él.

- **Nadie puede volver a selección de personaje/mapa después de una partida.** Los tres
  botones de `UI_MatchResults` (`GoToCharacterSelect`/`GoToStageSelect`) chequeaban
  `NetworkServer.active` antes de llamar `ServerChangeScene` - funciona por accidente en
  P2P (el host es su propio servidor), pero bajo GameLift *ningún* cliente es nunca el
  servidor, así que el botón no hacía nada para nadie. Mismo patrón que ya se había
  arreglado para la selección de nivel (`UI_MapSelectorOnline`/`LevelSelectRequestMessage`):
  ahora mandan `ReturnToCharacterSelectRequestMessage`/`ReturnToStageSelectRequestMessage`
  al servidor (`OnlineNetworkManager`), que es quien realmente llama `ServerChangeScene`.
  `GoToMainMenu` no tenía este problema - simplemente desconecta al que lo aprieta
  (`StopClient`), que es el comportamiento correcto en ambos modelos.

- **Un jugador que se une tarde (reconexión a mitad de partida) aparece fuera del mapa**
  (mayormente vacío/azul, con solo una esquina de tiles reales visible en el borde de
  cámara). Causa real: `OnlineNetworkManager.GetOnlineSpawnPosition` solo sabe la posición
  correcta (`LevelData.playerSpawnPositions`) para conexiones que estaban en
  `connectionSlots` - ese diccionario se llena una única vez, desde el lobby, *antes* de
  que arranque la partida original. Una conexión nueva (reconexión, o cualquier
  late-join) nunca está ahí, así que `slotIndex` da `-1` y el código caía al fallback
  genérico de Mirror (`startPositions`, vacío en este proyecto) o a un `Vector3` crudo
  armado con el `connectionId` (p.ej. `connectionId=7` → `Vector3(14, 0, 0)`) - una
  posición que fácilmente cae fuera del área jugable real. Arreglado usando
  `level.playerSpawnPositions` con módulo seguro sobre `connectionId` también para el caso
  sin slot, en vez de saltar directo al fallback genérico.
  - **Diagnóstico equivocado en el camino**: al ver el síntoma la primera vez, se
    sospechó que el nivel nunca se cargaba del lado del cliente que se reconecta (el
    broadcast `LevelSelectedMessage` de `SelectLevel()` es de una sola vez - una conexión
    que se une después de ese momento nunca lo recibe, así que `GameManager.currentLevel`
    le queda `null`). Ese *es* un gap real y se arregló igual
    (`CurrentLevelQueryMessage`/`OnlineNetworkManager.ClientLevelAssigned`,
    `LevelManager.Awake()` ahora espera la respuesta del servidor en vez de asumir que ya
    tiene el nivel), pero no era la causa del síntoma reportado - el nivel sí cargaba
    bien, el jugador solo aparecía spawneado lejos de él. Vale la pena tener ambos fixes:
    cubren gaps distintos del mismo patrón (estado que solo se manda una vez, nunca a
    quien se une después).

- **Timeout de desconexión (~5-10s) no es un bug.** Cuando un jugador cierra el cliente a
  la fuerza (Alt+F4, kill del proceso) no hay ningún paquete de aviso - UDP crudo no tiene
  "desconexión" a nivel protocolo. `PersonalizedTransport` solo puede notar que el cliente
  se fue cuando deja de recibir tráfico suyo por más de `timeoutDuration` (5s en el
  prefab) - así que un jugador desconectado sigue viéndose como conectado (slot ocupado en
  el lobby, etc.) hasta que ese timeout vence. Es esperable, no hace falta arreglarlo salvo
  que se quiera bajar `timeoutDuration` a costa de falsos positivos con lag real.

### Notas operativas del ciclo de fleet administrada

- **No se puede reemplazar el build de una fleet ya creada.** Cualquier fix de código del
  lado del servidor implica: rebuild (`GameLiftBuildScript.BuildWindowsServer`, con el
  Editor de Unity cerrado - batchmode falla con "multiple Unity instances" si no), `aws
  gamelift upload-build`, borrar la fleet vieja (`aws gamelift delete-fleet`, tarda ~2-4min
  en desaparecer de `describe-fleet-attributes`) y crear una nueva con el build nuevo. La
  cuenta solo permite **1 fleet EC2 por región** - hay que borrar la vieja antes de poder
  crear la nueva, no se pueden tener las dos a la vez.
- **Activación de una fleet nueva tarda ~8-12 minutos**, pasando por
  `NEW → DOWNLOADING → VALIDATING → BUILDING → ACTIVATING → ACTIVE` (a veces vuelve a
  `VALIDATING` un rato antes de `ACTIVATING` - normal, no es un error).
- **Fixes que solo tocan `GetOnlineSpawnPosition`/lógica server-only no necesitan rebuild
  del cliente** - ese código corre exclusivamente en `OnServerAddPlayer`, nunca en el
  proceso del jugador. Fixes que tocan código compartido usado también client-side
  (`LevelManager`, mensajes de `OnlineNetworkManager` que el cliente manda/recibe) sí
  necesitan que el jugador rebuildee su build también, o van a seguir viendo el bug viejo
  aunque el servidor ya esté arreglado.
- **Rutina entre sesiones de prueba**: escalar a 0 instancias deseadas
  (`aws gamelift update-fleet-capacity --desired-instances 0`) para no dejar nada
  consumiendo, y frenar `Tools/GameLiftBroker`/`Tools/SignalingServer` localmente. Para
  retomar: escalar a 1, esperar `ACTIVE` en `describe-fleet-capacity`, y recién ahí levantar
  el broker (`GAMELIFT_FLEET_ID` apuntando a la fleet actual - cambia cada vez que se
  recrea por un fix de servidor).
