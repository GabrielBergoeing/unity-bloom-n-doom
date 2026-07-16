# Firewall de Windows y el puerto del juego

## El hallazgo

Con el hole punching ya funcionando a nivel de router/NAT, las conexiones entre redes
distintas seguían fallando en los tres niveles (LAN, IP pública directa, y hole
punching) de la misma manera: el handshake nunca recibía respuesta. La causa no tenía
nada que ver con NAT/routers — era el **firewall de Windows en la PC que hostea**,
bloqueando el tráfico entrante al puerto del juego (7777/UDP) sin ningún aviso.

Windows normalmente muestra un popup automático la primera vez que una app escucha en
un puerto ("El Firewall de Windows Defender bloqueó algunas características de esta
app, ¿permitir?"). Pero esa notificación **viene desactivada por defecto en el perfil
de red "Público"** — en ese perfil, Windows bloquea en silencio, sin ningún indicio de
que algo esté mal. Coincide con que la misma red "Pública" ya nos había hecho ruido
antes con el descubrimiento de UPnP (ver `UPnP-Verification.md`).

Importante: esto es una capa **completamente separada** del router/NAT. Perforar el
NAT (hole punching) o abrir el puerto en el router (forwarding manual, UPnP) no tiene
ningún efecto sobre si Windows, en la propia PC, deja pasar ese tráfico una vez que ya
llegó a la placa de red.

## La solución automática

[WindowsFirewallHelper.cs](WindowsFirewallHelper.cs) se ejecuta al hostear
(`SteamLobby.HostDirect()`), solo en Windows y solo fuera del Editor:

1. Chequea (sin necesitar permisos de administrador) si ya existe una regla de entrada
   para el puerto del juego, usando `netsh advfirewall firewall show rule` y buscando
   el nombre de la regla en la salida — no depende del idioma del sistema.
2. Si no existe, pide **una sola vez** permiso de administrador (un popup UAC estándar
   de Windows) para crear la regla vía `netsh advfirewall firewall add rule`. El
   jugador ve un único diálogo "¿Permitir que esta app haga cambios?" — parecido al
   aviso automático que otros juegos disparan solos, pero acá lo pedimos nosotros
   explícitamente porque el perfil "Público" no lo muestra solo.
3. Si el jugador rechaza el permiso, o falla la creación, el juego sigue funcionando
   igual — simplemente sin esa regla (mismo comportamiento que antes de este cambio).

No hace nada en Mac/Linux, ni en el Editor (mismo criterio que `UpnpPortMapper` y
`HolePunchClient` — las pruebas en Editor usan loopback, no lo necesitan).

## Verificar/rehacer manualmente

Para confirmar que la regla quedó creada (como Administrador):

```powershell
netsh advfirewall firewall show rule name="BloomAndDoom-GamePort-7777"
```

Para crearla a mano si hiciera falta (reemplazando el puerto si usás uno distinto de
7777):

```powershell
New-NetFirewallRule -DisplayName "BloomAndDoom-GamePort-7777" -Direction Inbound -Protocol UDP -LocalPort 7777 -Action Allow
```

(Nota: el nombre que crea `netsh` y el `DisplayName` de `New-NetFirewallRule` son
conceptos ligeramente distintos en Windows, pero para efectos de esta regla simple
cualquiera de los dos caminos deja pasar el tráfico correctamente.)
