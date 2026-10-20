param(
    [Parameter(Mandatory = $true)][string]$GodotExe,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$CapturePath,
    [ValidateRange(1024, 3840)][int]$Width = 1440,
    [ValidateRange(720, 2160)][int]$Height = 900,
    [ValidateRange(1, 300)][int]$TimeoutSeconds = 90,
    [ValidateSet('Preview', 'VisualPreferencesStartup', 'SceneReview', 'ExplosivePreparation')][string]$Check = 'Preview'
)
$ErrorActionPreference = 'Stop'
$quietProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$quietCapture = [IO.Path]::GetFullPath($CapturePath)
$quietLog = [IO.Path]::ChangeExtension($quietCapture, 'stdout.log')
$quietErrorLog = [IO.Path]::ChangeExtension($quietCapture, 'stderr.log')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($quietCapture)) | Out-Null
Add-Type -Path (Join-Path $PSScriptRoot 'QuietGodot.cs')
$quietCheckArgument = switch ($Check) { 'VisualPreferencesStartup' { '--visual-preferences-smoke' } 'SceneReview' { '--scene-review-test' } 'ExplosivePreparation' { '--explosive-preparation-test' } default { '--preview-test' } }
$quietArguments = @('--path', $quietProject, '--resolution', ("{0}x{1}" -f $Width, $Height), '--audio-driver', 'Dummy', '--max-fps', '30', '--', $quietCheckArgument, '--capture', $quietCapture)
$quietResult = [QuietGodot]::Run((Resolve-Path -LiteralPath $GodotExe).Path, $quietProject, $quietArguments, $quietLog, $quietErrorLog, $TimeoutSeconds)
$quietResult | ConvertTo-Json
exit $quietResult.ExitCode
