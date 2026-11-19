#!/usr/bin/env bash
# One-time setup inside WSL2 Ubuntu (use this instead of setup_env.ps1 when Smart App
# Control stays on). Creates ~/plantdoc-venv with PyTorch for the RTX GPU.
#
#   bash /mnt/e/plant/training/setup_env.sh
set -euo pipefail
cd "$(dirname "$0")"
sudo apt-get update
sudo apt-get install -y python3-venv python3-pip
python3 -m venv ~/plantdoc-venv
source ~/plantdoc-venv/bin/activate
pip install --upgrade pip wheel
echo "Installing PyTorch for CUDA 12.8 (RTX 50-series), ~3 GB..."
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu128
pip install -r requirements.txt -r ../server/requirements.txt
python -c "import torch; ok=torch.cuda.is_available(); print('torch', torch.__version__, '| GPU:', torch.cuda.get_device_name(0) if ok else 'NOT FOUND')"
echo "Done. Each new terminal: source ~/plantdoc-venv/bin/activate && cd /mnt/e/plant/training"
