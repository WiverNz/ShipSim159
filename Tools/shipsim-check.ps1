[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Status','Compile','EditMode','PlayMode','Shakedown','FastCrafts','BuildCatalogue','PhaseOne','WaterWeather','Wake','Buoys','Menu')]
    [string]$Action,
    [string]$UnityEditor,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Use Windows PowerShell, including powershell.exe from WSL.'
}
$versionFile = Join-Path $project 'ProjectSettings\ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' }
if ($versionLine -notmatch '^m_EditorVersion: ([0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+)$') {
    throw 'Cannot read the required Unity version from ProjectVersion.txt.'
}
$version = $Matches[1]
$editor = $UnityEditor
if (!$editor) { $editor = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe" }
$projectPattern = [regex]::Escape($project.Replace('/', '\')).Replace('\\', '[\\/]')
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
    Where-Object { $_.CommandLine -match ('(?i)' + $projectPattern + '(?:[\\\s"'']|$)') })
if ($Action -eq 'Status') {
    # Omit import-worker command lines, which can contain licensing identifiers.
    $running | Select-Object ProcessId, Name, CreationDate | Format-Table
    if ($running.Count -eq 0) { Write-Output 'No Unity process for ShipSim159.' }
    exit 0
}
if ($running.Count -gt 0) { throw 'This project already has a Unity process. No second editor was started.' }
if (!(Test-Path -LiteralPath $editor -PathType Leaf)) { throw "Unity $version not found. Supply -UnityEditor with its absolute executable path." }
Set-Location -LiteralPath $project
$logDirectory = Join-Path $project 'Logs'
$resultDirectory = Join-Path $project 'TestResults'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$log = Join-Path $logDirectory ("check-$Action-$stamp.log")
$launchArgs = @('-batchmode','-buildTarget','Win64','-projectPath',$project,'-logFile',$log)
if ($Action -eq 'Compile') { $launchArgs += @('-nographics','-quit') }
elseif ($Action -in @('EditMode','PlayMode')) {
    $result = Join-Path $resultDirectory ("check-$Action-$stamp.xml")
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
# Start-Process joins its argument array; quote paths before passing that string.
$quotedArgs = @($launchArgs | ForEach-Object {
    if ($_ -match '["\r\n]') { throw 'Quotes and newlines are not supported in launch arguments.' }
    '"' + $_ + '"'
}) -join ' '
Write-Output "Log: $log"
if ($DryRun) {
    Write-Output "Editor: $editor"
    Write-Output "Arguments: $quotedArgs"
    exit 0
}
New-Item -ItemType Directory -Force $logDirectory, $resultDirectory | Out-Null
$run = Start-Process -FilePath $editor -ArgumentList $quotedArgs -Wait -PassThru
if ($run.ExitCode -ne 0) { exit $run.ExitCode }
if (!(Test-Path -LiteralPath $log)) { throw 'Unity exited without writing the expected log.' }
if (Select-String -LiteralPath $log -Pattern 'error [A-Z]' -CaseSensitive -Quiet) {
    throw "Unity reported compile errors. Read $log locally."
}
if ($Action -in @('EditMode','PlayMode')) {
    if (!(Test-Path -LiteralPath $result)) { throw 'Unity exited without test results.' }
    [xml]$xml = Get-Content -LiteralPath $result -Raw
    if ($xml.'test-run'.result -ne 'Passed') { throw "Tests did not pass. Read $result." }
}
exit 0
