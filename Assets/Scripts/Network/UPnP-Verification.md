# Verificar el mapeo automático de puerto (UPnP)

Guía para confirmar si `UpnpPortMapper` realmente abrió el puerto en un router real
(esto no se puede probar con ParrelSync/loopback - hace falta una segunda máquina en
otra red, o al menos otro dispositivo fuera de la red del host).

## 1. Logs a revisar en la consola de Unity (lado host)

Al hostear fuera del Editor (`autoPortForward` se salta dentro del Editor a propósito),
`SteamLobby.HostDirect()` dispara la corutina de `UpnpPortMapper`. Buscar, en orden:

| Log | Qué significa |
|---|---|
| `[SteamLobby] Iniciando Host directo...` | El host arrancó, se va a intentar el mapeo. |
| `[UPnP] No se encontró un router compatible, o UPnP está desactivado...` | Falló el descubrimiento SSDP (timeout de 3s). No respondió ningún router IGD - UPnP desactivado, red sin router administrable, o firewall bloqueando multicast (239.255.255.250:1900). |
| `[UPnP] El router rechazó el mapeo de puerto (...)` | Se encontró el router y se le pidió `AddPortMapping`, pero lo rechazó. Común en routers con UPnP "solo LAN" o con alguna política de seguridad que bloquea el WAN mapping via API. |
| `[SteamLobby] UPnP: puerto {port} abierto automáticamente.` | Éxito: el router confirmó el mapeo. |
| `[SteamLobby] UPnP: no se pudo abrir el puerto automáticamente...` | Resumen de fallo (acompaña a uno de los warnings de arriba). |

Si no aparece **ninguno** de estos logs, revisar que `autoPortForward` esté tildado en el
`Network Manager` de la escena y que no se esté corriendo desde el Editor (ahí se salta
a propósito).

## 2. Confirmar que el puerto realmente quedó abierto (fuera de Unity)

Que UPnP reporte éxito solo confirma que **tu propio router** aceptó el pedido - no
garantiza que el tráfico llegue desde internet (por ejemplo, si el ISP hace CGNAT, ver
más abajo). Para confirmar de punta a punta:

1. **La prueba real más confiable**: que alguien en **otra red** (no la misma WiFi -
   usar datos móviles, o un amigo real) intente unirse con el código generado. Si
   conecta, el mapeo funcionó de extremo a extremo.
2. **Panel del router**: entrar a la administración del router (típicamente
   `192.168.0.1` o `192.168.1.1`) y buscar la sección "UPnP" / "Port Forwarding" / "NAT".
   La mayoría de los routers domésticos listan ahí los mapeos activos - debería aparecer
   el puerto del juego (7777 por defecto) apuntando a la IP LAN de la PC que hostea, con
   la descripción `BloomNDoom-GameHost`.
3. **Checkeo de puerto externo**: herramientas como canyouseeme.org o portchecker.co
   permiten verificar si un puerto responde desde afuera - ojo que la mayoría de estas
   herramientas solo testean **TCP**, y el transporte de este proyecto usa **UDP**
   (KCP/PersonalizedTransport), así que no son 100% concluyentes acá; sirven más para
   confirmar que la IP pública detectada es correcta.

## 3. Fallas conocidas (no son bugs del código, son límites de la red)

- **CGNAT (Carrier-Grade NAT)**: común en conexiones móviles y algunos ISPs de
  fibra/cable. El router del jugador ni siquiera tiene una IP pública propia, así que
  ningún mapeo (automático o manual) puede funcionar. Señal de esto: UPnP puede reportar
  éxito igual (mapeó en tu router), pero la conexión externa sigue sin llegar. Para
  diagnosticarlo: comparar la IP pública que detecta el juego (o ipify.org) con la IP que
  el router muestra en su panel como "IP WAN" - si son distintas, hay CGNAT de por medio
  y no hay solución sin un relay o servidor dedicado (ver `GameLiftConnectionProvider`).
- **Redes corporativas/universitarias**: suelen bloquear SSDP/UPnP por política de
  seguridad - el descubrimiento nunca encuentra el router aunque exista.
- **UPnP desactivado en el router**: bastante común como hardening por defecto en routers
  más nuevos, o en firmwares custom (OpenWrt sin `miniupnpd`, por ejemplo).

En cualquiera de estos casos, la única alternativa hoy es el port forwarding manual (como
antes de este cambio); a futuro, GameLift elimina el problema por completo porque el
servidor ya tiene IP pública propia.
