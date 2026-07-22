# GameLift Broker

Servidor HTTP standalone, mínimo, que se para entre el juego y AWS GameLift. **Es el
único lugar que tiene las credenciales de AWS** — el cliente del juego nunca las ve.
El motivo: el flujo cliente que trae el plugin oficial de GameLift para fleets
*Anywhere* usa las credenciales IAM guardadas en `~/.aws/credentials` de la máquina, algo
que sirve para probar en el Editor pero que **no se puede meter en un build real** (se
podrían extraer del ejecutable). Este broker resuelve eso: guarda las credenciales de un
usuario IAM de permisos mínimos, y expone un único endpoint simple que el juego puede
llamar sin conocer nada de AWS.

Ver el protocolo comentado en `Program.cs`. Lo consume
`Assets/Scripts/Network/GameLiftConnectionProvider.cs` del lado de Unity.

## Requisitos

.NET 8 SDK (el mismo que ya instalaste para `Tools/SignalingServer`).

## Variables de entorno (obligatorias salvo que se indique lo contrario)

| Variable | Descripción |
|---|---|
| `GAMELIFT_FLEET_ID` | ID de la fleet Anywhere (`fleet-...`). |
| `GAMELIFT_LOCATION` | Ubicación custom donde está registrado el compute (`custom-...`). |
| `GAMELIFT_REGION` | Región de AWS, ej. `us-east-1`. |
| `GAMELIFT_MAX_PLAYERS` | Opcional, default `4`. |
| `GAMELIFT_BROKER_PORT` | Opcional, default `8090`. |

Las credenciales de AWS **no** son una variable propia de este proyecto — el SDK las
busca solo, en `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY` o en
`~/.aws/credentials`/`~/.aws/config` de esta máquina. Usar ahí el usuario IAM de
permisos mínimos (ver `aws iam create-user`/`put-user-policy` en las instrucciones de
setup que te haya dado el asistente), **no** tu usuario admin personal.

## Correrlo

**Importante (solo Windows)**: `HttpListener` necesita permiso para escuchar en todas
las interfaces de red (no solo `localhost`), y eso requiere administrador — o, mejor,
una reserva de URL que evita tener que correrlo elevado cada vez:

```powershell
# Una sola vez, como Administrador:
netsh http add urlacl url=http://+:8090/ user=Everyone
```

(Cambiá `8090` si usás otro `GAMELIFT_BROKER_PORT`.)

Después, para correrlo (ventana normal, sin necesitar admin gracias a la reserva de
arriba):

```powershell
$env:GAMELIFT_FLEET_ID = "fleet-..."
$env:GAMELIFT_LOCATION = "custom-..."
$env:GAMELIFT_REGION = "us-east-1"
cd Tools/GameLiftBroker
dotnet run
```

## Exponerlo a internet

Igual que `Tools/SignalingServer`: forwardear el puerto (TCP, no UDP esta vez — es HTTP)
en el router hacia esta PC, y agregar una regla de firewall de entrada:

```powershell
New-NetFirewallRule -DisplayName "GameLiftBroker" -Direction Inbound -Protocol TCP -LocalPort 8090 -Action Allow
```

Una vez corriendo y expuesto, poné `http://<tu-ip-pública>:8090` en el campo
`Broker Url` del componente `UI_GameLiftMenu` en Unity.

## Probarlo aislado (antes de tocar Unity)

Con el servidor dedicado ya corriendo en el compute registrado (lanzado vía
`Tools/GameLiftLauncher`, buscá en su log `"[GameLift] Proceso listo"`), con el broker
corriendo:

```powershell
curl -X POST http://localhost:8090/request-session -Body "{}" -ContentType "application/json"
```

Debería devolver algo como `{"success":true,"address":"...","port":7777,"playerSessionId":"psess-..."}`.
Si da `success:false` con el mensaje de timeout, confirmá que el servidor dedicado esté
realmente corriendo y haya llegado a `ActivateGameSession` antes de pedir la sesión.
