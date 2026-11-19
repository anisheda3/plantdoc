# ============================================================
#  AI Plant Doctor - one-shot repair
#  Fixes any amount of nested/duplicated folders inside Assets.
#
#  Run from your Unity project root (the folder with ProjectSettings):
#    powershell -ExecutionPolicy Bypass -File tools\REPAIR.ps1
# ============================================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=== AI Plant Doctor - repair ===" -ForegroundColor Cyan
Write-Host "Working in: $(Get-Location)"
Write-Host ""

if (-not (Test-Path "Assets")) {
    Write-Host "ERROR: no Assets folder here." -ForegroundColor Red
    Write-Host "Open your Unity project folder (the one containing ProjectSettings) and run again."
    exit 1
}
if (-not (Test-Path "ProjectSettings")) {
    Write-Host "ERROR: no ProjectSettings folder here." -ForegroundColor Red
    Write-Host "This does not look like a Unity project root."
    exit 1
}

# ------------------------------------------------------------
# 1. Find the best copy of each of the 4 real Unity folders.
#    "Best" = the one with the most .cs files.
# ------------------------------------------------------------
$needed = @("Editor", "Scripts", "Shaders", "Tests")
$best = @{}

$searchRoots = @(".", "Assets") + (Get-ChildItem "Assets" -Directory -ErrorAction SilentlyContinue |
                                   Where-Object { $_.Name -like "Assets*" } |
                                   ForEach-Object { $_.FullName })

foreach ($folder in $needed) {
    foreach ($root in $searchRoots) {
        $candidate = Join-Path $root $folder
        if (Test-Path $candidate) {
            $n = (Get-ChildItem $candidate -Filter *.cs -Recurse -ErrorAction SilentlyContinue).Count
            if ($folder -eq "Shaders") {
                $n = (Get-ChildItem $candidate -Filter *.shader -Recurse -ErrorAction SilentlyContinue).Count
            }
            if (-not $best.ContainsKey($folder) -or $n -gt $best[$folder].Count) {
                $best[$folder] = @{ Path = (Resolve-Path $candidate).Path; Count = $n }
            }
        }
    }
}

foreach ($folder in $needed) {
    if ($best.ContainsKey($folder)) {
        Write-Host ("  found {0,-8} ({1} files)" -f $folder, $best[$folder].Count) -ForegroundColor Green
    } else {
        Write-Host "  WARNING: could not find $folder anywhere" -ForegroundColor Yellow
    }
}

# Find a manifest.json for Packages
$manifest = $null
foreach ($p in @("Packages\manifest.json",
                 "Assets\Packages\manifest.json",
                 "Assets\Packages 1\manifest.json",
                 "Assets\Assets\Packages\manifest.json")) {
    if (Test-Path $p) { $manifest = (Resolve-Path $p).Path; break }
}

# Find docs / server / tools (prefer ones already at root)
$extras = @{}
foreach ($folder in @("docs", "server", "tools")) {
    foreach ($root in @(".", "Assets") + ($searchRoots | Select-Object -Skip 2)) {
        $candidate = Join-Path $root $folder
        if ((Test-Path $candidate) -and -not $extras.ContainsKey($folder)) {
            $extras[$folder] = (Resolve-Path $candidate).Path
        }
    }
}

# ------------------------------------------------------------
# 2. Stage the good copies outside Assets.
# ------------------------------------------------------------
$stage = Join-Path (Get-Location) "_repair_stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

foreach ($folder in $needed) {
    if ($best.ContainsKey($folder)) {
        Copy-Item $best[$folder].Path (Join-Path $stage $folder) -Recurse -Force
    }
}
foreach ($folder in $extras.Keys) {
    Copy-Item $extras[$folder] (Join-Path $stage "_$folder") -Recurse -Force
}
if ($manifest) {
    New-Item -ItemType Directory -Path (Join-Path $stage "_manifest") | Out-Null
    Copy-Item $manifest (Join-Path $stage "_manifest\manifest.json") -Force
}

Write-Host ""
Write-Host "  staged good copies" -ForegroundColor DarkGray

# ------------------------------------------------------------
# 3. Wipe Assets completely, then restore only what belongs.
# ------------------------------------------------------------
Remove-Item "Assets" -Recurse -Force
New-Item -ItemType Directory -Path "Assets" | Out-Null

foreach ($folder in $needed) {
    $src = Join-Path $stage $folder
    if (Test-Path $src) {
        Move-Item $src "Assets\$folder" -Force
        Write-Host "  Assets\$folder restored" -ForegroundColor Green
    }
}

# ------------------------------------------------------------
# 4. Put docs / server / tools at the project ROOT.
# ------------------------------------------------------------
foreach ($folder in @("docs", "server", "tools")) {
    $src = Join-Path $stage "_$folder"
    if (Test-Path $src) {
        if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
        Move-Item $src $folder -Force
        Write-Host "  $folder\ placed at project root" -ForegroundColor Green
    }
}

# ------------------------------------------------------------
# 5. Restore Packages\manifest.json at the ROOT.
# ------------------------------------------------------------
$stagedManifest = Join-Path $stage "_manifest\manifest.json"
if (Test-Path $stagedManifest) {
    if (-not (Test-Path "Packages")) { New-Item -ItemType Directory -Path "Packages" | Out-Null }
    Move-Item $stagedManifest "Packages\manifest.json" -Force
    Write-Host "  Packages\manifest.json restored" -ForegroundColor Green
}

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue

# ------------------------------------------------------------
# 6. Remove duplicate junk left at the root.
# ------------------------------------------------------------
foreach ($junk in @("Assets 1", "Packages 1", "docs 1", "server 1", "tools 1",
                    "Assets 2", "Packages 2")) {
    if (Test-Path $junk) { Remove-Item $junk -Recurse -Force; Write-Host "  removed $junk" -ForegroundColor DarkGray }
}

# ------------------------------------------------------------
# 7. Clear Unity's cache so it reimports cleanly.
# ------------------------------------------------------------
foreach ($cache in @("Library", "Temp", "obj")) {
    if (Test-Path $cache) {
        Remove-Item $cache -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  cleared $cache" -ForegroundColor DarkGray
    }
}

# ------------------------------------------------------------
# 8. Report
# ------------------------------------------------------------
Write-Host ""
Write-Host "Assets now contains:" -ForegroundColor Cyan
Get-ChildItem "Assets" -Name | ForEach-Object { Write-Host "    $_" }

Write-Host ""
Write-Host "Project root now contains:" -ForegroundColor Cyan
Get-ChildItem -Name | ForEach-Object { Write-Host "    $_" }

$scripts = (Get-ChildItem "Assets\Scripts" -Filter *.cs -Recurse -ErrorAction SilentlyContinue).Count
Write-Host ""
if ($scripts -ge 50) {
    Write-Host "OK - $scripts scripts in place. Reopen the project in Unity." -ForegroundColor Green
} else {
    Write-Host "Only $scripts scripts found (expected ~52)." -ForegroundColor Yellow
    Write-Host "Re-copy the Assets folder from the workspace, then run this again."
}
Write-Host ""
