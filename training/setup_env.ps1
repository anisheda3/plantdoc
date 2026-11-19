# One-time setup: creates training\.venv with PyTorch for your RTX GPU plus the
# training and server libraries, then checks that the GPU is visible.
#
#   powershell -ExecutionPolicy Bypass -File training\setup_env.ps1
#
# Needs Smart App Control OFF (otherwise Windows blocks PyTorch's DLLs) - or use
# setup_env.sh inside WSL2 instead.
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$venv = Join-Path $here ".venv"
$py = Join-Path $venv "Scripts\python.exe"
$serverReqs = Join-Path (Split-Path $here -Parent) "server\requirements.txt"

if (-not (Test-Path $py)) {
    Write-Host "Creating virtual environment (Python 3.11)..." -ForegroundColor Cyan
    py -3.11 -m venv $venv
}
& $py -m pip install --upgrade pip wheel

Write-Host "Installing PyTorch for CUDA 12.8 (needed by RTX 50-series GPUs, ~3 GB)..." -ForegroundColor Cyan
& $py -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128

Write-Host "Installing training and server libraries..." -ForegroundColor Cyan
& $py -m pip install -r (Join-Path $here "requirements.txt") -r $serverReqs

& $py -c "import torch; ok=torch.cuda.is_available(); print('torch', torch.__version__, '| CUDA', torch.version.cuda, '| GPU:', torch.cuda.get_device_name(0) if ok else 'NOT FOUND'); x=torch.randn(512,512,device='cuda') if ok else None; print('GPU matmul ok' if ok and (x@x).isfinite().all() else 'GPU test skipped')"
Write-Host "Done. Activate with:  training\.venv\Scripts\Activate.ps1" -ForegroundColor Green
