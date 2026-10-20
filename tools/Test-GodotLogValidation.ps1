$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'GodotLogValidation.ps1')
$fixtureRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\.local\godot-log-test-' + [Guid]::NewGuid().ToString('N'))))
[void][IO.Directory]::CreateDirectory($fixtureRoot)
$stdoutPath = Join-Path $fixtureRoot 'stdout.log'
$stderrPath = Join-Path $fixtureRoot 'stderr.log'
$cases = @(
    @{ Name = 'clean markers'; Out = "PREVIEW_TEST_OK: verified`n"; Err = ''; Reject = $false },
    @{ Name = 'expected preference failures are data'; Out = "VISUAL_PREFERENCES_SAVE_FAILED: denied`nVISUAL_PREFERENCES_UNAVAILABLE: malformed`nPREVIEW_TEST_OK: verified`n"; Err = ''; Reject = $false },
    @{ Name = 'runtime error despite success marker'; Out = 'PREVIEW_TEST_OK: verified'; Err = 'ERROR: detached node'; Reject = $true },
    @{ Name = 'script error on stdout'; Out = "SCRIPT ERROR: invalid call`nPREVIEW_TEST_OK: verified"; Err = ''; Reject = $true },
    @{ Name = 'unhandled managed exception'; Out = 'PREVIEW_TEST_OK: verified'; Err = 'Unhandled exception. System.InvalidOperationException'; Reject = $true },
    @{ Name = 'resource leak'; Out = 'PREVIEW_TEST_OK: verified'; Err = 'WARNING: ObjectDB instances were leaked at exit'; Reject = $true },
    @{ Name = 'marker mentioned inside other text is not success'; Out = 'did not reach PREVIEW_TEST_OK'; Err = ''; Reject = $true }
)
try {
    foreach ($case in $cases) {
        [IO.File]::WriteAllText($stdoutPath, $case.Out); [IO.File]::WriteAllText($stderrPath, $case.Err)
        $rejected = $false
        try { Assert-GodotValidationLog -StandardOutputPath $stdoutPath -StandardErrorPath $stderrPath -RequiredMarkers @('PREVIEW_TEST_OK') }
        catch { $rejected = $true }
        if ($rejected -ne $case.Reject) { throw ('Incorrect log validation: ' + $case.Name) }
    }
    Write-Output ($cases.Count.ToString() + ' Godot log validation groups passed.')
}
finally {
    if ([IO.File]::Exists($stdoutPath)) { [IO.File]::Delete($stdoutPath) }
    if ([IO.File]::Exists($stderrPath)) { [IO.File]::Delete($stderrPath) }
    [IO.Directory]::Delete($fixtureRoot)
}
