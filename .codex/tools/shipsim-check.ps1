[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Status','Compile','EditMode','PlayMode','Shakedown','FastCrafts','BuildCatalogue','PhaseOne','WaterWeather','Wake','Buoys','Menu')]
    [string]$Action
)
$ErrorActionPreference = 'Stop'
$project = 'G:\Projects\ShipSim159'
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -match '(?i)G:[\\/]Projects[\\/]ShipSim159(?:[\\/\s"'']|$)' })
if ($Action -eq 'Status') {
    # Omit import-worker command lines, which can contain licensing identifiers.
    $running | Select-Object ProcessId, Name, CreationDate | Format-Table
    if ($running.Count -eq 0) { Write-Output 'No Unity process for ShipSim159.' }
    exit 0
}
if ($running.Count -gt 0) { throw 'This project already has a Unity process. No second editor was started.' }
if (!(Test-Path -LiteralPath $editor)) { throw 'Configured Unity editor was not found.' }
Set-Location -LiteralPath $project
$version = Get-Content -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt')
if (!($version -match '^m_EditorVersion: 6000\.6\.0f1$')) { throw 'Editor version changed; review this launcher before running it.' }
$logDirectory = Join-Path $project 'Logs'
$resultDirectory = Join-Path $project 'TestResults'
New-Item -ItemType Directory -Force $logDirectory, $resultDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$log = Join-Path $logDirectory ("autoallow-$Action-$stamp.log")
$launchArgs = @('-batchmode','-buildTarget','Win64','-projectPath',$project,'-logFile',$log)
if ($Action -eq 'Compile') { $launchArgs += @('-nographics','-quit') }
elseif ($Action -in @('EditMode','PlayMode')) {
    $result = Join-Path $resultDirectory ("autoallow-$Action-$stamp.xml")
    $launchArgs += @('-runTests','-testPlatform',$Action,'-testResults',$result)
    Write-Output "Results: $result"
}
else {
    $methods = @{
        Shakedown = 'ShipSimulator.Editor.VesselShakedownCheck.Run'
        FastCrafts = 'ShipSimulator.Editor.VesselShakedownCheck.RunFastCrafts'
        BuildCatalogue = 'ShipSimulator.Editor.VesselCatalogueBuilder.Build'
        PhaseOne = 'ShipSimulator.Editor.GraphicsPhaseOneCheck.Run'
        WaterWeather = 'ShipSimulator.Editor.WaterWeatherCheck.Run'
        Wake = 'ShipSimulator.Editor.ShipWakeRuntimeCheck.Run'
        Buoys = 'ShipSimulator.Editor.BuoyGraphicsCheck.Run'
        Menu = 'ShipSimulator.Editor.VoyageMenuSmokeCheck.Run'
    }
    $launchArgs += @('-executeMethod', $methods[$Action])
    if ($Action -eq 'BuildCatalogue') { $launchArgs += '-quit' }
}
Write-Output "Log: $log"
$run = Start-Process -FilePath $editor -ArgumentList $launchArgs -Wait -PassThru
exit $run.ExitCode
