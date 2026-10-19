param(
    [Parameter(Mandatory = $true)][string]$GodotExe,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$CapturePath,
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 90
)
$ErrorActionPreference = 'Stop'
$quietProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$quietCapture = [IO.Path]::GetFullPath($CapturePath)
$quietLog = [IO.Path]::ChangeExtension($quietCapture, 'stdout.log')
$quietErrorLog = [IO.Path]::ChangeExtension($quietCapture, 'stderr.log')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($quietCapture)) | Out-Null
Add-Type -Path (Join-Path $PSScriptRoot 'QuietGodot.cs')
$quietArguments = @('--path', $quietProject, '--resolution', '1440x900', '--audio-driver', 'Dummy', '--max-fps', '30', '--', '--preview-test', '--capture', $quietCapture)
$quietResult = [QuietGodot]::Run((Resolve-Path -LiteralPath $GodotExe).Path, $quietProject, $quietArguments, $quietLog, $quietErrorLog, $TimeoutSeconds)
$quietResult | ConvertTo-Json
exit $quietResult.ExitCode
