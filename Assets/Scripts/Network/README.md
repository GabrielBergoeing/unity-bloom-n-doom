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
desde su propia PC, un servidor dedicado (build `UNITY_SERVER`) corre en una fleet
**administrada** de AWS GameLift. GameLift devuelve un `{ip/dnsName, port}` de sesión más
un `PlayerSessionId`, mismo "shape" que ya produce `ConnectionInfo` (`address`, `port`,
`sessionToken`). El `sessionToken` se reenvía automáticamente a
`GameLiftPlayerAuthenticator.clientPlayerSessionId` desde
`SteamLobby.ApplyRuntimeLaunchRequest`, sin tocar la UI ni `SteamLobby`.

Piezas:

| Archivo | Rol |
|---|---|
| `UI_GameLiftMenu.cs` | UI: botón "Conectar", pide una sesión al broker y navega a `CharacterSelectorOnline`. `brokerUrl` vacío = no-op seguro hasta desplegar el broker. |
| `GameLiftConnectionProvider.cs` | POST `{brokerUrl}/request-session`, devuelve `ConnectionInfo` (timeout configurable, 35s por defecto - el broker puede tardar hasta 30s creando una sesión nueva). |
| `Tools/GameLiftBroker` | Proceso .NET separado, el único que tiene credenciales AWS; habla con `AmazonGameLiftClient` (crear/buscar `GameSession`, crear `PlayerSession`). Ver su propio README para el despliegue. |
| `GameLiftServerManager.cs` (`#if UNITY_SERVER`) | Corre solo en el build de servidor dedicado. `InitSDK()` sin parámetros (fleet administrada - GameLift inyecta todo vía su propio agente en la instancia, no hace falta token manual). Arranca Mirror al recibir `OnStartGameSession`. |
| `GameLiftPlayerAuthenticator.cs` | `NetworkAuthenticator` en el `Network Manager` prefab: valida el `PlayerSessionId` contra GameLift en servidores `UNITY_SERVER`; en cualquier otro build (host P2P normal) acepta automáticamente, sin cambiar el comportamiento existente. |

Requisitos de infraestructura AWS (fuera de este repo, hay que provisionarlos/mantenerlos
aparte): una fleet administrada con un build subido, el broker corriendo en algún lugar
alcanzable 24/7 por los jugadores (puerto propio, no el de hole punching), y las
credenciales AWS del broker con permisos mínimos de GameLift
(`CreateGameSession`/`DescribeGameSessions`/`CreatePlayerSession`).
