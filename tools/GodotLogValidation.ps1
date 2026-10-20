function Assert-GodotValidationLog {
    param(
        [Parameter(Mandatory = $true)][string]$StandardOutputPath,
        [Parameter(Mandatory = $true)][string]$StandardErrorPath,
        [string[]]$RequiredMarkers = @()
    )
    $validationOutput = [IO.File]::ReadAllText($StandardOutputPath)
    $validationError = [IO.File]::ReadAllText($StandardErrorPath)
    $failurePattern = '(?im)^\s*(?:(?:SCRIPT )?ERROR:|Unhandled exception|WARNING:.*(?:leak|ObjectDB|RID))[^\r\n]*'
    foreach ($logText in @($validationError, $validationOutput)) {
        $failure = [regex]::Match($logText, $failurePattern)
        if ($failure.Success) { throw ('Godot log failure: ' + $failure.Value.Trim()) }
    }
    foreach ($marker in $RequiredMarkers) {
        $markerPattern = '(?m)^' + [regex]::Escape($marker) + '(?=:|\s|$)'
        if (-not [regex]::IsMatch($validationOutput, $markerPattern)) { throw "Godot log is missing '$marker'." }
    }
}
