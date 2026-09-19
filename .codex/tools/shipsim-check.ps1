[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Status','Compile','EditMode','PlayMode','Shakedown','FastCrafts','BuildCatalogue','PhaseOne','WaterWeather','Wake','Buoys','Menu')]
    [string]$Action
)
$ErrorActionPreference = 'Stop'
$project = 'G:\Projects\ShipSim159'
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$version = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt')
if (!($version -match '^m_EditorVersion: 6000\.6\.0f1$')) { throw 'Editor version changed; review this launcher before running it.' }
& (Join-Path $project 'Tools\shipsim-check.ps1') -Action $Action -UnityEditor $editor
exit $LASTEXITCODE
