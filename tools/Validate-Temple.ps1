[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GodotExe,
    [Parameter(Mandatory = $true)][string]$DataDirectory,
    [ValidateRange(10, 300)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GodotLogValidation.ps1')
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$godotPath = (Resolve-Path -LiteralPath $GodotExe).ProviderPath
$dataPath = (Resolve-Path -LiteralPath $DataDirectory).ProviderPath
if (-not (Test-Path -LiteralPath $godotPath -PathType Leaf)) { throw 'GodotExe must name an existing executable.' }
if (-not (Test-Path -LiteralPath (Join-Path $dataPath 'master.dat') -PathType Leaf)) { throw 'DataDirectory must contain master.dat.' }
$dotnetPath = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
$runName = 'temple-validation-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$outputPath = Join-Path (Join-Path $projectRoot '.local') $runName
[void][IO.Directory]::CreateDirectory($outputPath)
$steps = [Collections.Generic.List[object]]::new()
$manifestPath = Join-Path $outputPath 'manifest.json'
$manifest = [ordered]@{ Version = 1; Mode = 'headless'; StartedUtc = [DateTime]::UtcNow.ToString('o'); Project = $projectRoot;
    DataDirectory = $dataPath; GodotExe = $godotPath; Passed = $false; Steps = $steps; Failure = $null }

# Windows native quoting, including trailing backslashes. No shell command strings are evaluated.
function ConvertTo-NativeArgument([string]$Value) {
    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') { return $Value }
    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Invoke-ValidationStep([string]$Name, [string]$Executable, [string[]]$Arguments, [string[]]$Markers = @(), [switch]$JsonReport, [switch]$CheckGodotLogs) {
    $stdout = Join-Path $outputPath ($Name + '.stdout.log')
    $stderr = Join-Path $outputPath ($Name + '.stderr.log')
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $row = [ordered]@{ Name = $Name; ExitCode = $null; TimedOut = $false; Passed = $false; Milliseconds = 0; StandardOutput = $stdout; StandardError = $stderr }
    $steps.Add($row)
    $process = $null
    try {
        $nativeArguments = ($Arguments | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' '
        $process = Start-Process -FilePath $Executable -ArgumentList $nativeArguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        [void]$process.Handle
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $row.TimedOut = $true
            # This is only the exact process created for this validation step.
            try { $process.Kill(); $process.WaitForExit() } catch { }
            throw "Validation step '$Name' timed out."
        }
        $row.ExitCode = $process.ExitCode
        if ($process.ExitCode -ne 0) { throw "Validation step '$Name' exited with code $($process.ExitCode)." }
        $output = [IO.File]::ReadAllText($stdout)
        foreach ($marker in $Markers) { if (-not $output.Contains($marker)) { throw "Validation step '$Name' did not emit '$marker'." } }
        if ($CheckGodotLogs) { Assert-GodotValidationLog -StandardOutputPath $stdout -StandardErrorPath $stderr -RequiredMarkers $Markers }
        if ($JsonReport) {
            $report = $output | ConvertFrom-Json
            if ($report.OriginalReconstruction -ne 'verified') { throw "Source reconstruction was not verified by '$Name'." }
        }
        $row.Passed = $true
    }
    finally { $watch.Stop(); $row.Milliseconds = $watch.ElapsedMilliseconds; if ($null -ne $process) { $process.Dispose() } }
}

try {
    $powershellPath = (Get-Command powershell.exe -CommandType Application -ErrorAction Stop).Source
    Invoke-ValidationStep 'godot-log-validator' $powershellPath @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Test-GodotLogValidation.ps1')) @('Godot log validation groups passed.')
    Invoke-ValidationStep 'build-preview' $dotnetPath @('build', (Join-Path $projectRoot 'Fallout Remake.csproj'))
    Invoke-ValidationStep 'build-inspector' $dotnetPath @('build', (Join-Path $projectRoot 'tools/TempleInspector/TempleInspector.csproj'))
    $inspector = Join-Path $projectRoot 'tools/TempleInspector/bin/Debug/net8.0/TempleInspector.dll'
    Invoke-ValidationStep 'synthetic' $dotnetPath @($inspector, '--self-test') @('synthetic test groups passed.')
    foreach ($reportName in @('temple-chest', 'temple-dialogue', 'temple-state', 'item-origin-route', 'item-art', 'temple-collection', 'temple-barrier', 'explosive-profile', 'item-use-checkpoint', 'barrier-effect', 'barrier-commit')) {
        Invoke-ValidationStep $reportName $dotnetPath @($inspector, '--data-dir', $dataPath, ('--' + $reportName + '-report')) -JsonReport
    }
    # No graphical fallback: this command always passes --headless.
    Invoke-ValidationStep 'godot-preview' $godotPath @('--headless', '--path', $projectRoot, '--', '--data-dir', $dataPath, '--preview-test') @(
        'PREVIEW_TEST_OK', 'ITEM_CHECKPOINT_OK', 'BAG_ORIGIN_OK', 'SCRIPTED_CONTAINER_OK', 'INVENTORY_ART_UI_OK', 'ELEVATION_WALK_GUARD_OK', 'PLACED_USE_PREVIEW_OK', 'BARRIER_PLACEMENT_GUIDANCE_OK', 'EXPLOSIVE_PREPARATION_UI_OK', 'USE_CLOCK_UI_OK', 'BARRIER_EFFECT_UI_OK', 'TEMPLE_EXPLOSIVE_PREVIEW_AUDIT_OK', 'APPLIED_BARRIER_PREVIEW_OK', 'TAKE_ALL_UI_OK', 'RETURN_ALL_UI_OK', 'INITIAL_GATE_PREVIEW_OK', 'TEMPLE_SPEECH_UI_OK', 'TEMPLE_SPEECH_SAVE_UI_OK', 'TEMPLE_NPC_APPROACH_OK', 'ENTITY_READABILITY_OK', 'SCENE_REVIEW_OK', 'CONTAINER_LOCATOR_OK', 'VISUAL_PREFERENCES_OK') -CheckGodotLogs
    $manifest.Passed = $true
}
catch { $manifest.Failure = $_.Exception.Message }
finally {
    $manifest.CompletedUtc = [DateTime]::UtcNow.ToString('o')
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}
[PSCustomObject]@{ Passed = $manifest.Passed; StepCount = $steps.Count; Manifest = $manifestPath; Failure = $manifest.Failure } | ConvertTo-Json
if (-not $manifest.Passed) { exit 1 }
exit 0
