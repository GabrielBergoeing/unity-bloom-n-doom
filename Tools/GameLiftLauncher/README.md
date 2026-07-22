# GameLift Launcher

Wrapper de una línea para lanzar el build `UNITY_SERVER` contra la fleet Anywhere
existente (`fleet-72471bbe-...`, compute `Cito-WindowsPC`). El token de autenticación de
un compute Anywhere expira cada ~15 minutos, así que hay que pedir uno nuevo con
`aws gamelift get-compute-auth-token` antes de cada arranque - este script lo hace solo
en vez de tener que correr el comando a mano y copiar el token cada vez.

## Requisitos

AWS CLI configurado con credenciales que tengan permiso `gamelift:GetComputeAuthToken` y
`gamelift:DescribeCompute` sobre la fleet.

## Uso

```powershell
cd Tools/GameLiftLauncher
.\Start-GameLiftServer.ps1
```

Por defecto busca el build en `Builds/GameLiftServerWindows/BloomAndDoomServer.exe`
(relativo a la raíz del repo). Para usar otro path:

```powershell
.\Start-GameLiftServer.ps1 -ServerExePath "C:\ruta\a\BloomAndDoomServer.exe"
```

El script deja el proceso corriendo en primer plano - dejar la ventana abierta mientras
el compute deba estar disponible para partidas. Como el token dura ~15 minutos, si el
proceso se cae o se reinicia hace falta volver a correr el script (pide un token nuevo
automáticamente al arrancar).
