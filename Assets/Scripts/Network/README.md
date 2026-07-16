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
| `GameLiftConnectionProvider.cs` | Stub, no conectado todavía - ver sección GameLift abajo. |
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

## GameLift (a futuro, no implementado)

`GameLiftConnectionProvider` es un stub sin lógica todavía. La idea, cuando se retome:
GameLift devuelve un `{ip/dnsName, port}` de sesión más un `PlayerSessionId` — mismo
"shape" que ya produce `ConnectionInfo` hoy (`address`, `port`, `sessionToken`). El
`sessionToken` ya se reenvía automáticamente a `GameLiftPlayerAuthenticator` desde
`SteamLobby.ApplyRuntimeLaunchRequest`, así que conectar ese proveedor futuro no debería
requerir tocar la UI ni `SteamLobby` — solo implementar `GameLiftConnectionProvider` y
elegirlo en `UI_OnlineDirectMenu` en vez de `JoinCodeConnectionProvider`.
