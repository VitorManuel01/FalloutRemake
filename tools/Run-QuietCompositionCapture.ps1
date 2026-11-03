param(
    [string]$GodotExe = "D:\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe",
    [string]$ProjectPath = "C:\Dev\FalloutRemake",
    [string]$ScriptPath = "res://Assets/Testes/ProjectMojaveBatch01/capture_composition.gd",
    [int]$Width = 1440,
    [int]$Height = 900,
    [int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'QuietGodot.cs')

$quietProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$outLog = Join-Path $PSScriptRoot '..\.local\composition_capture.stdout.log'
$errLog = Join-Path $PSScriptRoot '..\.local\composition_capture.stderr.log'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outLog)) | Out-Null

$quietArgs = @(
    '--path', $quietProject,
    '--resolution', ("{0}x{1}" -f $Width, $Height),
    '--audio-driver', 'Dummy',
    '--max-fps', '30',
    '-s', $ScriptPath
)

$res = [QuietGodot]::Run((Resolve-Path -LiteralPath $GodotExe).Path, $quietProject, $quietArgs, $outLog, $errLog, $TimeoutSeconds)
$res | ConvertTo-Json
if ($res.ExitCode -ne 0) { exit 1 }
exit 0
