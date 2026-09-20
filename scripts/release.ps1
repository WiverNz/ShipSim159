# Keep argument spelling compatible with release.sh, including double-hyphen options.
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Show-Usage {
    @'
Usage: .\scripts\release.ps1 [patch|minor|major|X.Y.Z] [options]
Create an annotated release tag on the current commit. Default bump: patch.
  -n, --dry-run       Print the plan without changing anything (allows a dirty tree).
  -y, --yes           Skip confirmation.
      --push          Push the current branch to origin, then the new tag.
  -m, --message TEXT  Tag message (default: Release vX.Y.Z).
  -h, --help          Show this help.
Versions come from the highest local vX.Y.Z tag, or 0.0.0 if none exist.
Fetch origin's tags before releasing. This script never edits files or commits.
'@
}

function Test-Version([string]$Value) {
    return $Value -cmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z'
}

function Invoke-ReleaseGit {
    param([string[]]$GitArguments, [switch]$AllowFailure)
    $output = @(& git @GitArguments)
    $code = $LASTEXITCODE
    if ($code -ne 0 -and !$AllowFailure) {
        throw "git $($GitArguments -join ' ') failed (exit $code)"
    }
    return [pscustomobject]@{ Output = $output; ExitCode = $code }
}

try {
    $spec = ''
    $message = ''
    $dryRun = $false
    $assumeYes = $false
    $doPush = $false
    for ($i = 0; $i -lt $args.Count; $i++) {
        $argument = [string]$args[$i]
        switch -CaseSensitive ($argument) {
            { $_ -ceq '-h' -or $_ -ceq '--help' } { Show-Usage; exit 0 }
            { $_ -ceq '-n' -or $_ -ceq '--dry-run' } { $dryRun = $true; break }
            { $_ -ceq '-y' -or $_ -ceq '--yes' } { $assumeYes = $true; break }
            '--push' { $doPush = $true; break }
            { $_ -ceq '-m' -or $_ -ceq '--message' } {
                $i++
                if ($i -ge $args.Count) { throw "$argument requires text" }
                $message = [string]$args[$i]
                break
            }
            default {
                if ($argument.StartsWith('-')) { throw "unknown option: $argument" }
                if ($spec) { throw 'multiple versions supplied' }
                $spec = $argument
            }
        }
    }

    Push-Location -LiteralPath (Join-Path $PSScriptRoot '..')
    try {
        $head = Invoke-ReleaseGit @('rev-parse', '--verify', 'HEAD') -AllowFailure
        if ($head.ExitCode -ne 0) { throw 'no committed HEAD' }
        $branchCheck = Invoke-ReleaseGit @('symbolic-ref', '--quiet', 'HEAD') -AllowFailure
        if ($branchCheck.ExitCode -ne 0) { throw 'detached HEAD' }
        $shallow = Invoke-ReleaseGit @('rev-parse', '--is-shallow-repository')
        if (($shallow.Output -join '') -cne 'false') { throw 'fetch full history and tags first' }
        $status = Invoke-ReleaseGit @('status', '--porcelain')
        if ($status.Output.Count -gt 0) {
            if (!$dryRun) { throw 'working tree is dirty; review and commit changes before releasing' }
            [Console]::Error.WriteLine('warning: working tree is dirty; a real release would be refused')
        }

        $current = '0.0.0'
        $tags = Invoke-ReleaseGit @('for-each-ref', '--sort=-version:refname', '--format=%(refname:strip=2)', 'refs/tags/v*')
        foreach ($tag in $tags.Output) {
            if ([string]$tag -cnotmatch '\Av') { continue }
            $candidate = ([string]$tag).Substring(1)
            if (Test-Version $candidate) { $current = $candidate; break }
        }
        $parts = @($current.Split('.') | ForEach-Object { [bigint]::Parse($_) })
        if (!$spec) { $spec = 'patch' }
        switch -CaseSensitive ($spec) {
            'major' { $new = '{0}.0.0' -f ($parts[0] + 1) }
            'minor' { $new = '{0}.{1}.0' -f $parts[0], ($parts[1] + 1) }
            'patch' { $new = '{0}.{1}.{2}' -f $parts[0], $parts[1], ($parts[2] + 1) }
            default { $new = $spec }
        }
        if (!(Test-Version $new)) { throw "expected a stable SemVer X.Y.Z without leading zeros: $new" }
        $newParts = @($new.Split('.') | ForEach-Object { [bigint]::Parse($_) })
        $greater = $false
        for ($i = 0; $i -lt 3; $i++) {
            if ($newParts[$i] -ne $parts[$i]) {
                $greater = $newParts[$i] -gt $parts[$i]
                break
            }
        }
        if (!$greater) { throw "version must be greater than $current" }
        $existing = Invoke-ReleaseGit @('show-ref', '--verify', '--quiet', "refs/tags/v$new") -AllowFailure
        if ($existing.ExitCode -eq 0) { throw "tag v$new already exists" }
        if ($existing.ExitCode -ne 1) { throw 'could not check existing release tag' }
        if ($doPush) {
            $remote = Invoke-ReleaseGit @('remote', 'get-url', 'origin') -AllowFailure
            if ($remote.ExitCode -ne 0) { throw 'origin remote is required for --push' }
        }
        if (!$message) { $message = "Release v$new" }
        $commit = Invoke-ReleaseGit @('rev-parse', 'HEAD')
        Write-Output "Current: $current"
        Write-Output "Release: v$new"
        Write-Output "Commit: $($commit.Output -join '')"
        Write-Output "Message: $message"
        Write-Output "Push to origin: $([int]$doPush)"
        if ($dryRun) {
            Write-Output 'Dry run: no files, refs or remotes changed.'
            exit 0
        }
        if (!$assumeYes) {
            $answer = Read-Host 'Create this release tag? [y/N]'
            if ($answer -cnotmatch '\A[Yy]\z') { throw 'aborted' }
        }

        $result = Invoke-ReleaseGit @('tag', '-a', "v$new", '-m', $message)
        $result.Output | Write-Output
        if ($doPush) {
            $branch = Invoke-ReleaseGit @('symbolic-ref', '--short', 'HEAD')
            $result = Invoke-ReleaseGit @('push', 'origin', ($branch.Output -join ''))
            $result.Output | Write-Output
            $result = Invoke-ReleaseGit @('push', 'origin', "refs/tags/v${new}:refs/tags/v$new")
            $result.Output | Write-Output
        }
        else {
            Write-Output "Push when ready: git push origin refs/tags/v${new}:refs/tags/v$new"
        }
    }
    finally {
        Pop-Location
    }
}
catch {
    [Console]::Error.WriteLine("error: $($_.Exception.Message)")
    exit 1
}
exit 0
