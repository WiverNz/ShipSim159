# Exercises the launcher without starting Unity or reading real process command lines.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('shipsim tools ' + [guid]::NewGuid())
New-Item -ItemType Directory -Force (Join-Path $fixture 'Tools'), (Join-Path $fixture 'ProjectSettings') | Out-Null
try {
    Set-Content (Join-Path $fixture 'ProjectSettings\ProjectVersion.txt') 'm_EditorVersion: 6000.6.0f1'
    $fakeEditor = Join-Path $fixture 'Unity.exe'
    Set-Content $fakeEditor ''
    $source = Get-Content (Join-Path $PSScriptRoot 'shipsim-check.ps1') -Raw
    $mock = @'
function Get-CimInstance {
    if ($env:SHIPSIM_TOOL_TEST -eq 'busy') {
        [pscustomobject]@{ CommandLine = 'Unity.exe -projectPath "' + $project + '"'; ProcessId = 123 }
    }
}
function Start-Process {
    param($FilePath, $ArgumentList, [switch]$Wait, [switch]$PassThru)
    if ($ArgumentList -notlike ('*"' + $project + '"*')) { throw 'Project path lost its quotes.' }
    if ($Action -eq 'PlayMode' -and $ArgumentList -match '-quit|-nographics') { throw 'Invalid test flags.' }
    if ($env:SHIPSIM_TOOL_TEST -ne 'missing-log') {
        $text = if ($env:SHIPSIM_TOOL_TEST -eq 'compile-error') { 'error UAC0020: fixture' } else { '0 error count' }
        Set-Content -LiteralPath $log -Value $text
    }
    if ($Action -in @('EditMode','PlayMode') -and $env:SHIPSIM_TOOL_TEST -ne 'missing-xml') {
        $outcome = if ($env:SHIPSIM_TOOL_TEST -eq 'failed-test') { 'Failed' } else { 'Passed' }
        Set-Content -LiteralPath $result -Value ('<test-run result="' + $outcome + '"/>')
    }
    $code = if ($env:SHIPSIM_TOOL_TEST -eq 'exit-error') { 7 } else { 0 }
    [pscustomobject]@{ ExitCode = $code }
}
'@
    $script = Join-Path $fixture 'Tools\shipsim-check.ps1'
    $source = $source.Replace("`$ErrorActionPreference = 'Stop'", $mock + "`n`$ErrorActionPreference = 'Stop'")
    Set-Content -LiteralPath $script -Value $source
    $savedTestEnvironment = $env:SHIPSIM_TOOL_TEST
    $hostExe = (Get-Process -Id $PID).Path
    $cases = @(
        @('ok','Compile',0), @('ok','EditMode',0), @('ok','PlayMode',0),
        @('busy','Compile',1), @('missing-log','Compile',1),
        @('compile-error','Compile',1), @('exit-error','Compile',7),
        @('missing-xml','PlayMode',1), @('failed-test','EditMode',1), @('ok','Invalid',1)
    )
    foreach ($case in $cases) {
        $env:SHIPSIM_TOOL_TEST = $case[0]
        # Each invocation needs fresh outputs, including the missing-output cases.
        Get-ChildItem -LiteralPath $fixture -Directory | Where-Object Name -in @('Logs','TestResults') |
            Remove-Item -Recurse -Force
        $ErrorActionPreference = 'Continue'
        $output = & $hostExe -NoProfile -ExecutionPolicy RemoteSigned -File $script -Action $case[1] -UnityEditor $fakeEditor 2>&1
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne $case[2]) { throw "Case $case returned $LASTEXITCODE instead of $($case[2]): $output" }
        Write-Output "PASS: $case"
    }
} finally {
    $env:SHIPSIM_TOOL_TEST = $savedTestEnvironment
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
