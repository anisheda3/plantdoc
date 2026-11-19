"""Builds contact sheets of docs/screenshots (8 per sheet, 4 columns) for quick review: sheetA.png, sheetB.png, sheetC.png."""
import glob, sys
from PIL import Image
files = sorted(glob.glob(r"E:\plant\docs\screenshots\*.png"))
out = sys.argv[1]
def sheet(names, path, cols=4, w=360):
    ims = [Image.open(f).convert("RGB") for f in names]
    h = int(w * ims[0].height / ims[0].width)
    rows = (len(ims) + cols - 1) // cols
    S = Image.new("RGB", (cols * w, rows * h), (255, 255, 255))
    for i, im in enumerate(ims):
        S.paste(im.resize((w, h)), ((i % cols) * w, (i // cols) * h))
    S.save(path)
for n, (a, b) in enumerate([(0, 8), (8, 16), (16, 99)]):
    if files[a:b]: sheet(files[a:b], f"{out}\sheet{'ABC'[n]}.png")
