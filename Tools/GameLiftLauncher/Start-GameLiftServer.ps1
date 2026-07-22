# Launches the UNITY_SERVER build against an existing GameLift Anywhere compute,
# fetching a fresh auth token first - Anywhere tokens expire (~15 min), so the manual
# "aws gamelift get-compute-auth-token" dance has to happen before every single launch.
# This script automates that instead of doing it by hand each time.
#
# Usage:
#   .\Start-GameLiftServer.ps1
#   .\Start-GameLiftServer.ps1 -ServerExePath "C:\path\to\BloomAndDoomServer.exe"

param(
    [string]$FleetId = "fleet-72471bbe-1659-4eb0-a38a-c3075fcf6de1",
    [string]$ComputeName = "Cito-WindowsPC",
    [string]$Region = "us-east-1",
    [string]$ServerExePath = "$PSScriptRoot\..\..\Builds\GameLiftServerWindows\BloomAndDoomServer.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ServerExePath)) {
    Write-Error "No se encontro el build del servidor en: $ServerExePath"
    exit 1
}

Write-Host "Pidiendo auth token fresco para compute '$ComputeName' en fleet '$FleetId'..."
$tokenResult = aws gamelift get-compute-auth-token --fleet-id $FleetId --compute-name $ComputeName --region $Region | ConvertFrom-Json
$computeInfo = aws gamelift describe-compute --fleet-id $FleetId --compute-name $ComputeName --region $Region | ConvertFrom-Json

$env:GAMELIFT_WEBSOCKET_URL = $computeInfo.Compute.GameLiftServiceSdkEndpoint
$env:GAMELIFT_HOST_ID = $ComputeName
$env:GAMELIFT_FLEET_ID = $FleetId
$env:GAMELIFT_AUTH_TOKEN = $tokenResult.AuthToken

Write-Host "Token obtenido (expira $($tokenResult.ExpirationTimestamp))."
Write-Host "Lanzando $ServerExePath ..."

& $ServerExePath -batchmode -nographics
