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
