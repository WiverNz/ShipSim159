[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executable = Join-Path $project 'build\CameraReview\ShipSim159.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build LocalWindowsBuild.RunCameraReview first.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $project ('Logs\Camera\native-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$folder = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $folder) { throw 'Use a fresh output folder; probe evidence is not overwritten.' }
New-Item -ItemType Directory -Path $folder | Out-Null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class CameraProbeMouse {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left, top, right, bottom; }
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static void Send(IntPtr window, uint flags, int x, int y, int data) {
        if (GetForegroundWindow() != window) throw new InvalidOperationException("Probe window lost focus; input stopped.");
        mouse_event(flags, unchecked((uint)x), unchecked((uint)y), unchecked((uint)data), UIntPtr.Zero);
    }
}
'@
$previous = [CameraProbeMouse]::GetForegroundWindow()
$cursor = New-Object CameraProbeMouse+Point
[void][CameraProbeMouse]::GetCursorPos([ref]$cursor)
$run = $null
$window = [IntPtr]::Zero
try {
    $arguments = @('--camera-input-probe','--camera-output',$folder,'-screen-fullscreen','0',
        '-screen-width','1280','-screen-height','720','-logFile',(Join-Path $folder 'player.log'))
    $quoted = ($arguments | ForEach-Object { if ($_ -match '["\r\n]') { throw 'Invalid argument.' }; '"' + $_ + '"' }) -join ' '
    $run = Start-Process -FilePath $executable -WorkingDirectory $project -ArgumentList $quoted -PassThru
    $deadline = (Get-Date).AddSeconds(60)
    while (!(Test-Path -LiteralPath (Join-Path $folder 'ready.txt'))) {
        if ($run.HasExited -or (Get-Date) -gt $deadline) { throw 'Player did not reach input-ready state.' }
        Start-Sleep -Milliseconds 200
    }
    $run.Refresh(); $window = $run.MainWindowHandle
    if ($window -eq [IntPtr]::Zero) { throw 'No player window.' }
    [void][CameraProbeMouse]::SetForegroundWindow($window)
    $rect = New-Object CameraProbeMouse+Rect
    [void][CameraProbeMouse]::GetWindowRect($window, [ref]$rect)
    [void][CameraProbeMouse]::SetCursorPos(($rect.left + $rect.right) / 2, ($rect.top + $rect.bottom) / 2)
    Start-Sleep -Milliseconds 500
    [CameraProbeMouse]::Send($window, 0x0800, 0, 0, 120)
    Start-Sleep -Milliseconds 700
    [CameraProbeMouse]::Send($window, 0x0800, 0, 0, -120)
    Start-Sleep -Milliseconds 700
    for ($i = 0; $i -lt 8; $i++) { [CameraProbeMouse]::Send($window, 0x0800, 0, 0, 15); Start-Sleep -Milliseconds 20 }
    Start-Sleep -Milliseconds 700
    [CameraProbeMouse]::Send($window, 0x0800, 0, 0, -120)
    Start-Sleep -Milliseconds 700
    [CameraProbeMouse]::Send($window, 0x0008, 0, 0, 0)
    Start-Sleep -Milliseconds 150
    for ($i = 0; $i -lt 20; $i++) { [CameraProbeMouse]::Send($window, 0x0001, 5, 0, 0); Start-Sleep -Milliseconds 16 }
    Start-Sleep -Milliseconds 500
    [CameraProbeMouse]::Send($window, 0x0010, 0, 0, 0)
    Start-Sleep -Milliseconds 300
    [CameraProbeMouse]::Send($window, 0x0008, 0, 0, 0)
    Start-Sleep -Milliseconds 150
    for ($i = 0; $i -lt 10; $i++) { [CameraProbeMouse]::Send($window, 0x0001, 0, 5, 0); Start-Sleep -Milliseconds 16 }
    Start-Sleep -Milliseconds 500
    [CameraProbeMouse]::Send($window, 0x0010, 0, 0, 0)
    if (!$run.WaitForExit(30000)) { throw 'Player input probe timed out.' }
    if ($run.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $folder 'done.txt'))) { throw 'Player input probe failed.' }
    Write-Output "Native input trace: $folder"
}
finally {
    if ($window -ne [IntPtr]::Zero -and [CameraProbeMouse]::GetForegroundWindow() -eq $window) {
        [CameraProbeMouse]::Send($window, 0x0010, 0, 0, 0)
        [void][CameraProbeMouse]::SetForegroundWindow($previous)
        [void][CameraProbeMouse]::SetCursorPos($cursor.x, $cursor.y)
    }
    if ($run -and !$run.HasExited) { [void]$run.CloseMainWindow(); if (!$run.WaitForExit(5000)) { $run.Kill() } }
}
