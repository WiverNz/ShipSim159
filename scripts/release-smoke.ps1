$ErrorActionPreference = 'Stop'
$pkgDir = 'build/StandaloneWindows64'

try {
    for ($i = 0; $i -lt $args.Count; $i++) {
        $argument = [string]$args[$i]
        switch -CaseSensitive ($argument) {
            '--pkg-dir' {
                $i++
                if ($i -ge $args.Count -or ![string]$args[$i]) {
                    [Console]::Error.WriteLine('--pkg-dir requires a directory')
                    exit 2
                }
                $pkgDir = [string]$args[$i]
                break
            }
            { $_ -ceq '-h' -or $_ -ceq '--help' } {
                Write-Output 'Usage: .\scripts\release-smoke.ps1 [--pkg-dir DIRECTORY]'
                Write-Output 'Checks Windows Mono player files, without launching the game.'
                exit 0
            }
            default {
                [Console]::Error.WriteLine("Unknown argument: $argument")
                exit 2
            }
        }
    }

    $files = @(
        'ShipSim159.exe'
        'UnityPlayer.dll'
        'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'
        'ShipSim159_Data/boot.config'
        'ShipSim159_Data/globalgamemanagers'
        'ShipSim159_Data/Managed/ShipSimulator.Runtime.dll'
    )
    foreach ($file in $files) {
        $path = Join-Path $pkgDir $file
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "missing or empty Windows build file: $path"
        }
    }
    Write-Output "RELEASE_SMOKE|PASS: $pkgDir"
}
catch {
    [Console]::Error.WriteLine("error: $($_.Exception.Message)")
    exit 1
}
exit 0
