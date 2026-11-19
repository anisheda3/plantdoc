# Runs the real Unity test suites headlessly (no Editor window needed).
#
#   powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1            # EditMode + PlayMode
#   powershell -ExecutionPolicy Bypass -File tools\run-tests.ps1 -Screens   # also re-capture docs/screenshots
#
# Close the Unity Editor first: a project can only be opened by one Unity instance.
param(
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe",
    [switch]$Screens,
    [ValidateSet('classic','dashboard')][string]$Style = 'classic'   # which interface the scene was built with
)

$project = Split-Path $PSScriptRoot -Parent
$out = Join-Path $project "Logs\TestResults"
New-Item -ItemType Directory -Force $out | Out-Null

function Run-Suite($platform, $filter) {
    $xml = Join-Path $out "$platform.xml"
    $unityArgs = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $platform,
              '-testResults', $xml, '-logFile', (Join-Path $out "$platform.log"))
    if ($filter) { $unityArgs += @('-testFilter', $filter) }
    $p = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru -Wait
    if (-not (Test-Path $xml)) { Write-Host "$platform : no results (exit $($p.ExitCode)) - see $out\$platform.log" -ForegroundColor Red; return $false }
    [xml]$r = Get-Content $xml
    $run = $r.'test-run'
    $color = if ($run.failed -eq '0') { 'Green' } else { 'Red' }
    Write-Host ("{0,-9} total {1}  passed {2}  failed {3}" -f $platform, $run.total, $run.passed, $run.failed) -ForegroundColor $color
    $r.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { Write-Host "  FAIL $($_.fullname)" -ForegroundColor Red }
    return $run.failed -eq '0'
}

$ok = Run-Suite 'EditMode' $null
if ($Style -eq 'classic') { $flowTest = 'PlantDoctor.Tests.ClassicFlowTests.ClassicFlowRunsEndToEnd'; $shotTest = 'PlantDoctor.Tests.ClassicFlowTests.CaptureClassicScreenshots' }
else { $flowTest = 'PlantDoctor.Tests.PhoneScanFlowTests.PhoneScanFlowRunsEndToEnd'; $shotTest = 'PlantDoctor.Tests.PhoneScanFlowTests.CaptureStageScreenshots' }
$ok = (Run-Suite 'PlayMode' $flowTest) -and $ok
if ($Screens) { $ok = (Run-Suite 'PlayMode' $shotTest) -and $ok }

if ($ok) { exit 0 } else { exit 1 }
