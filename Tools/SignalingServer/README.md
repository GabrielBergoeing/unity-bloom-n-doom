# Signaling Server (NAT hole punching)

Servidor UDP standalone, mínimo, que ayuda a un host y un cliente (cada uno detrás de
su propio NAT) a descubrirse y perforar un agujero para conectarse directamente entre
sí (P2P real). **Nunca ve tráfico del juego** — solo intercambia direcciones IP:puerto
en mensajes de texto muy chicos. No es un servidor dedicado ni un relay.

Ver el protocolo comentado en `Program.cs`. Lo usa
`Assets/Scripts/Network/HolePunchClient.cs` del lado de Unity.

## Requisitos

.NET 8 SDK (o superior). Verificar con `dotnet --version`.

## Correrlo localmente (pruebas)

```
cd Tools/SignalingServer
dotnet run
```

Escucha en el puerto UDP **9050** por defecto (ver `Port` en `Program.cs`).

## Desplegarlo con IP pública

Este proceso necesita ser alcanzable por internet en el puerto 9050/UDP desde ambas
puntas (host y cliente) — es la única pieza de esta arquitectura que sí necesita una
IP pública fija. Opciones simples:

1. **Un VPS/servidor propio** (facultad, casa con IP fija, un free-tier de Oracle
   Cloud/Fly.io/Render, etc.): copiar esta carpeta, `dotnet publish -c Release`, correr
   el binario, y abrir/forwardear el puerto 9050/UDP en su firewall/router.
2. **Local, temporal, para pruebas**: correrlo en cualquier PC con el puerto 9050/UDP
   forwardeado manualmente en su router — solo esa PC (la del servidor de señalización)
   necesita el forwarding, no los jugadores.

Una vez desplegado, hay que configurar su dirección (IP/dominio + puerto) en el campo
`Signaling Server Address` del componente `HolePunchClient` en el Network Manager de
Unity (`Assets/Scripts/Network/HolePunchClient.cs`). Si se deja vacío, el hole punching
queda deshabilitado automáticamente y el flujo cae de nuevo al intento de IP pública
directa (sin perforar NAT) como antes.

## Métricas centralizadas (NetworkMetrics)

Además del protocolo UDP, este proceso levanta un servidor HTTP aparte (puerto **9051**
por defecto, configurable con la variable de entorno `SIGNALING_METRICS_PORT`) con un
único endpoint: `POST /metrics?room=<roomCode>&role=<Host|Client>&player=<netId>`, body =
el CSV crudo. Lo usa `Assets/Scripts/NetworkMetrics.cs` para subir su CSV automáticamente
al terminar cada partida P2P (además de guardarlo local en
`Application.persistentDataPath`, que sigue pasando siempre). Los archivos quedan en
`Tools/SignalingServer/metrics/` (o al lado del ejecutable si corriste `dotnet publish`),
nombrados `<room>_<role>_<player>_<timestamp>.csv` — todo junto en un solo lugar, sin
andar copiando archivos de cada PC de prueba a mano.

Es un no-op silencioso si `HolePunchClient` no está configurado (p.ej. sesiones GameLift o
LAN sin hole punching) — el CSV local se sigue guardando igual, solo no se sube a ningún
lado.

**Para exponerlo junto con el puerto UDP**: agregar también el puerto TCP 9051 al
forwarding del router (además del 9050/UDP), y en Windows:

```powershell
# Una sola vez, como Administrador (igual que con Tools/GameLiftBroker):
netsh http add urlacl url=http://+:9051/ user=Everyone
New-NetFirewallRule -DisplayName "SignalingServerMetrics" -Direction Inbound -Protocol TCP -LocalPort 9051 -Action Allow
```
