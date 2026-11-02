param(
    [Parameter(Mandatory = $true)][string]$GodotExe,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$CapturePath,
    [ValidateRange(1024, 3840)][int]$Width = 1440,
    [ValidateRange(720, 2160)][int]$Height = 900,
    [ValidateRange(1, 900)][int]$TimeoutSeconds = 90,
    [ValidateSet('Preview', 'VisualPreferencesStartup', 'SceneReview', 'ExplosivePreparation', 'Nature')][string]$Check = 'Preview'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GodotLogValidation.ps1')
$quietProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$quietCapture = [IO.Path]::GetFullPath($CapturePath)
$quietLog = [IO.Path]::ChangeExtension($quietCapture, 'stdout.log')
$quietErrorLog = [IO.Path]::ChangeExtension($quietCapture, 'stderr.log')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($quietCapture)) | Out-Null
Add-Type -Path (Join-Path $PSScriptRoot 'QuietGodot.cs')
$quietCheckArgument = switch ($Check) { 'VisualPreferencesStartup' { '--visual-preferences-smoke' } 'SceneReview' { '--scene-review-test' } 'ExplosivePreparation' { '--explosive-preparation-test' } 'Nature' { '--nature-test' } default { '--preview-test' } }
$quietArguments = @('--path', $quietProject, '--resolution', ("{0}x{1}" -f $Width, $Height), '--audio-driver', 'Dummy', '--max-fps', '30', '--', $quietCheckArgument, '--capture', $quietCapture)
$quietResult = [QuietGodot]::Run((Resolve-Path -LiteralPath $GodotExe).Path, $quietProject, $quietArguments, $quietLog, $quietErrorLog, $TimeoutSeconds)
$quietMarkers = switch ($Check) {
    'VisualPreferencesStartup' { @('VISUAL_PREFERENCES_STARTUP_OK') }
    'SceneReview' { @('ENTITY_READABILITY_OK', 'SCENE_REVIEW_OK', 'CONTAINER_LOCATOR_OK') }
    'Nature' { @('NATURE_MAP_OK', 'NATURE_TREES_OK', 'NATURE_PICKING_OK', 'NATURE_ASSET_FALLBACK_OK', 'NATURE_MODELS_OK') }
    'ExplosivePreparation' { @('BARRIER_PLACEMENT_GUIDANCE_OK', 'BARRIER_EFFECT_UI_OK', 'USE_CLOCK_UI_OK', 'EXPLOSIVE_PREPARATION_UI_OK', 'APPLIED_BARRIER_PREVIEW_OK', 'TAKE_ALL_UI_OK', 'RETURN_ALL_UI_OK', 'INITIAL_GATE_PREVIEW_OK', 'TEMPLE_DUEL_OK', 'TEMPLE_KEY_OK', 'TEMPLE_SPEECH_UI_OK', 'TEMPLE_SPEECH_SAVE_UI_OK', 'TEMPLE_NPC_APPROACH_OK', 'TEMPLE_NPC_DEPARTURE_UI_OK', 'TEMPLE_NPC_DEPARTURE_3D_OK', 'TEMPLE_ACTIVE_SPEECH_OK', 'TEMPLE_NPC_AUTOMATIC_OK', 'TEMPLE_NPC_ROUTE_OK', 'TEMPLE_VILLAGE_ROUTE_OK', 'ARROYO_ENTRY_REWARD_OK', 'ARROYO_ENTRY_GLOBALS_OK', 'ARROYO_RETURN_LOOP_OK', 'ARROYO_ELDER_APPROACH_OK', 'ARROYO_ELDER_CONSULTATION_OK') }
    default { @('PREVIEW_TEST_OK', 'TEMPLE_ARCHITECTURE_OK', 'COMBAT_SLICE_OK', 'COMBAT_LEAVE_OK', 'POISON_OK', 'WANDER_OK', 'CRITICAL_OK', 'HAND_OK', 'COMBAT_BAG_OK', 'CALLED_SHOT_OK', 'THROW_OK', 'PERK_OK', 'REST_OK', 'CHARACTER_CREATION_OK', 'SKILL_SHEET_OK', 'VISUAL_PREFERENCES_OK', 'TAKE_ALL_UI_OK', 'RETURN_ALL_UI_OK', 'INITIAL_GATE_PREVIEW_OK', 'TEMPLE_DUEL_OK', 'TEMPLE_KEY_OK', 'TEMPLE_SPEECH_UI_OK', 'TEMPLE_SPEECH_SAVE_UI_OK', 'TEMPLE_NPC_APPROACH_OK', 'TEMPLE_NPC_DEPARTURE_UI_OK', 'TEMPLE_NPC_DEPARTURE_3D_OK', 'TEMPLE_ACTIVE_SPEECH_OK', 'TEMPLE_NPC_AUTOMATIC_OK', 'TEMPLE_NPC_ROUTE_OK', 'TEMPLE_VILLAGE_ROUTE_OK', 'ARROYO_ENTRY_REWARD_OK', 'ARROYO_ENTRY_GLOBALS_OK', 'ARROYO_RETURN_LOOP_OK', 'ARROYO_ELDER_APPROACH_OK', 'ARROYO_ELDER_CONSULTATION_OK') }
}
$quietLogFailure = $null
try { Assert-GodotValidationLog -StandardOutputPath $quietLog -StandardErrorPath $quietErrorLog -RequiredMarkers $quietMarkers }
catch { $quietLogFailure = $_.Exception.Message }
$quietPassed = $quietResult.ExitCode -eq 0 -and -not $quietResult.TimedOut -and $null -eq $quietLogFailure -and $quietResult.InputDesktopBefore -eq $quietResult.InputDesktopAfter
$quietResult | Add-Member -NotePropertyName Passed -NotePropertyValue $quietPassed
$quietResult | Add-Member -NotePropertyName LogFailure -NotePropertyValue $quietLogFailure
$quietResult | ConvertTo-Json
if (-not $quietPassed) { exit 1 }
exit 0
