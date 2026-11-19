# Verifies the AI Plant Doctor folder layout. Run from the Unity project root:
#   powershell -File tools\check-layout.ps1
$errorCount = 0
$warnCount  = 0

Write-Host "AI Plant Doctor - layout check"
Write-Host "Running in: $(Get-Location)"
Write-Host "------------------------------------------------------------"

if (-not (Test-Path "Assets")) {
    Write-Host "FAIL  No Assets/ folder here."
    Write-Host "      Run this from the Unity project root."
    exit 1
}
if (-not (Test-Path "ProjectSettings")) {
    Write-Host "WARN  No ProjectSettings/ - is this really the Unity project root?"
    $warnCount++
}

# CRITICAL: fake UnityEngine must not be inside Assets/
$stub = Get-ChildItem -Path "Assets" -Filter "UnityStub.cs" -Recurse -ErrorAction SilentlyContinue
if ($stub) {
    Write-Host "FAIL  UnityStub.cs found INSIDE Assets/ - this WILL break compilation."
    $stub | ForEach-Object { Write-Host "        $($_.FullName)" }
    Write-Host "      Fix: move the whole tools/ folder to the project root."
    $errorCount++
} else {
    Write-Host "OK    No UnityStub.cs inside Assets/ (critical check passed)"
}

foreach ($d in @("Scripts","Editor","Tests","Shaders")) {
    if (Test-Path "Assets\$d") { Write-Host "OK    Assets/$d present" }
    else { Write-Host "FAIL  Assets/$d is MISSING"; $errorCount++ }
}

foreach ($d in @("tools","server","docs")) {
    if (Test-Path "Assets\$d") {
        Write-Host "FAIL  Assets/$d should be at the PROJECT ROOT, not inside Assets/"
        $errorCount++
    }
}

foreach ($d in @("server","tools","docs")) {
    if (Test-Path $d) { Write-Host "OK    $d/ at project root (correctly hidden from Unity)" }
    else { Write-Host "INFO  $d/ not present at root - optional"; $warnCount++ }
}

$asmdefs = @(
    "Assets\Scripts\PlantDoctor.Runtime.asmdef",
    "Assets\Editor\PlantDoctor.Editor.asmdef",
    "Assets\Tests\EditMode\PlantDoctor.Tests.asmdef"
)
foreach ($f in $asmdefs) {
    if (Test-Path $f) { Write-Host "OK    $(Split-Path $f -Leaf)" }
    else { Write-Host "FAIL  Missing $f"; $errorCount++ }
}

if (Test-Path "Packages\manifest.json") {
    if (Select-String -Path "Packages\manifest.json" -Pattern "universal" -Quiet) {
        Write-Host "OK    Packages/manifest.json includes URP"
    } else {
        Write-Host "WARN  manifest.json has no URP entry - shader may render pink"
        $warnCount++
    }
} else {
    Write-Host "FAIL  Packages/manifest.json missing"
    $errorCount++
}

$count = (Get-ChildItem -Path "Assets\Scripts" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue).Count
if ($count -ge 50) { Write-Host "OK    $count runtime scripts found" }
else { Write-Host "WARN  Only $count runtime scripts (expected ~52)"; $warnCount++ }

Write-Host "------------------------------------------------------------"
if ($errorCount -eq 0) {
    Write-Host "PASS - layout is correct. ($warnCount warning(s))"
    Write-Host "Next: open Unity, then Tools > AI Plant Doctor > Build Demo Data"
    exit 0
} else {
    Write-Host "FAILED - $errorCount problem(s). See docs/FOLDER_STRUCTURE.md"
    exit 1
}
