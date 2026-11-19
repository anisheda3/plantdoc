# LiDAR and RGB-D for Crop 3D Reconstruction and Depth — Findings

*Summary of a discussion on 28 Oct 2026. Sensor figures are typical values from general knowledge and were not checked against current datasheets. Confirm specs and prices before buying hardware.*

## Verdict

| Job | Possible? | Verdict |
|---|---|---|
| **3D reconstruction of a crop** | **Yes** | Rows and canopies can be reconstructed, but only the outer surface. The inner canopy stays hidden unless you scan from several angles. |
| **3D reconstruction of a single plant** | **Yes** | Best with close-range RGB-D, structured light or photogrammetry / Gaussian splatting. |
| **Depth capture of a crop** | **Yes** | Every sensor class below can do it. Accuracy depends on range and lighting. |
| **Fine details on the plant** | **Partly** | Millimetre to sub-millimetre detail is possible only at close range (7–50 cm). Most fine features, such as lesions and veins, are captured by the **colour layer** wrapped onto the 3D model, not by depth. Microscopic features (hairs/trichomes, pores) are **not possible** with LiDAR or RGB-D. |

**Key rule:** no single sensor gives both whole-field coverage and fine detail. The working approach is **multi-scale**: LiDAR for the crop, close-range RGB-D or scanners for plants and leaves, and high-resolution colour images for surface detail.

## Area vs. detail

| Distance | Coverage | Detail | Typical sensor |
|---|---|---|---|
| 10–100 m | Whole field | 1–5 cm | Drone or vehicle LiDAR (e.g. DJI Zenmuse L2, Livox) |
| 1–5 m | Rows, single plants | 0.5–2 cm | Phone LiDAR, ground LiDAR, RealSense D455 |
| 0.3–1 m | One plant, its stems and leaves | 1–5 mm | RGB-D (RealSense D435, Orbbec), stereo (ZED 2i, OAK-D) |
| 7–50 cm | Leaf, fruit, stem | 0.1–1 mm | RealSense D405, structured-light scanners (Artec, Zivid, Photoneo), laser line scanners (Phenospex PlantEye) |
| Under 5 cm | Hairs, pores, fungal growth | Microns | Macro photography or a microscope, not depth sensors |

## Sensor notes

| Sensor | Strengths | Weaknesses |
|---|---|---|
| Phone LiDAR (iPhone/iPad Pro) | Built in, true scale, room-scale mesh | Low resolution (about 256×192 depth); canopy shape only, no plant detail |
| Software depth on Android (ARCore Depth API) | No extra hardware | Coarse, noisy, loses thin leaves |
| Active-stereo RGB-D (Intel RealSense) | Works reasonably outdoors; good close range (D405) | Stray "flying pixels" at edges; struggles with thin stems |
| Time-of-flight RGB-D (Orbbec Femto, Azure Kinect, now discontinued) | Very accurate indoors | **Fails in direct sunlight**; errors on wet or translucent leaves |
| Stereo cameras (ZED 2i, OAK-D) | Good outdoors | ZED needs an NVIDIA GPU; less detail than structured light |
| Structured-light or laser scanners | Sub-millimetre detail | Indoor or controlled light, slow, expensive |
| Drone or ground LiDAR | Field scale, true scale | Too coarse for leaf-level detail |

## Feasibility of individual measurements

| Detail | Possible? | How |
|---|---|---|
| Plant or canopy height | Yes, reliable | Any LiDAR or RGB-D sensor. Lodging (flattened cereals) shows up as a drop in height |
| Canopy volume and cover | Yes | LiDAR or RGB-D. Leaf area index (LAI) is only a rough estimate |
| Biomass | Partly | Calculated from volume; needs calibration for each crop |
| Plant counting | Yes | RGB plus depth; easiest at early growth stages |
| Stem thickness, branch angles | Yes, up close | RGB-D at under 1 m; stems thinner than about 3 mm break up |
| Leaf shape, area, angle, curl | Yes, small plants only | Close RGB-D or structured light; dense canopy hides inner leaves |
| Fruit count, size, shape | Yes | RGB-D (used commercially in orchards); hidden fruit has to be estimated |
| Disease spots and lesions | Yes, **from the colour image** | They're flat colour changes; colour wrapped onto the 3D model gives size in real mm² |
| Raised or sunken lesions (scab, cankers) | Partly | Structured light or laser at under 30 cm can pick up about 0.1–0.5 mm of relief |
| Leaf veins | Colour yes; depth only weakly | Visible in the colour texture; their relief is at the limit of the best scanners |
| Wilting, drooping, leaf rolling | Yes | Shape change over time, a genuine strength of depth |
| Growth tracking | Yes | Repeat scans and compare height and volume |
| Hairs, pores, early fungal growth | **No** | Needs macro or microscope imaging |
| Disease before symptoms show | **Not from depth** | Needs multispectral, hyperspectral or thermal cameras, which can be layered onto the 3D model |

## Practical limits

- **Sunlight:** rules out time-of-flight outdoors. Use active stereo, passive stereo or LiDAR.
- **Wind:** moving leaves blur the model. Capture fast, use a windbreak, or scan in calm conditions.
- **Plants hiding each other:** the inner canopy is never seen. Scan from several angles.
- **Thin edges and stems:** produce flying pixels and gaps, so the data needs filtering.
- **Shiny, wet or translucent leaves:** cause depth errors, especially for time-of-flight.

## Gaussian splatting

- **What it is:** a method (published 2023) that turns many ordinary photos or a video into a photorealistic 3D scene viewable in real time. The scene is made of millions of small, soft, see-through 3D blobs ("Gaussians"), each with a position, shape, opacity and a colour that can change with viewing angle. The blobs are optimised until rendered views match the photos.
- **Good for plants:** handles thin and fuzzy structures (leaf edges, stems) better than photogrammetry meshes, keeps lesions and veins at photo quality, and needs only a phone camera (apps: Polycam, Luma, Scaniverse).
- **Limitations:**
  - It isn't a solid surface, so measuring is harder (variants such as 2D Gaussian Splatting and SuGaR extract meshes).
  - It has **no real-world scale** without a ruler, marker, or LiDAR or RGB-D depth.
  - The plant must stay still.
  - It needs a GPU or cloud processing.
  - Stray floating blobs appear in poorly covered areas.
- **Best pairing:** Gaussian splatting for appearance plus LiDAR or RGB-D for true scale (depth-supervised splatting).

| Method | Output | Plant detail | Measurement-ready |
|---|---|---|---|
| Photogrammetry | Textured mesh | Good on solid parts, poor on thin ones | Yes |
| NeRF | Neural network | Very good, slow to render | Hard |
| Gaussian splatting | Millions of blobs | Very good, fast | Partly (needs a scale reference or depth) |
| LiDAR / RGB-D | Depth points | Limited by sensor resolution | Yes, true scale |

## Recommended setup for "crop plus fine detail"

1. **Crop level:** drone or ground LiDAR, or photogrammetry, for height, volume, plant count and growth maps.
2. **Plant level:** an RGB-D camera (RealSense D435 or D405) at 0.3–1 m, moved around the plant or mounted on a rig. Merge the views into one model (Open3D or the camera's SDK).
3. **Detail level:** a high-resolution colour camera, or photogrammetry / Gaussian splatting, wrapped onto the model for lesions and veins measured in real units.
4. **Optional:** multispectral or thermal imaging layered on top for early stress or disease.

## Possible additions to Plant Doctor

From the first part of the discussion, ranked by value against effort. Items 1–4 need only the Android software depth from ARCore, with no new hardware.

1. Remove the background using depth, to address the colour tracer's confusion with green backgrounds and shadows.
2. Show lesion and leaf size in real units (mm² / cm²) instead of the unreliable "% of leaf" figure.
3. Use real distance for the "move closer / move back" scan check.
4. Let real plants hide the 3D plant in AR (depth occlusion).
5. An iOS LiDAR mode for bed or row height and canopy volume (needs an iOS build).
6. A scanned 3D twin of the user's own plant from photogrammetry or Gaussian splatting (processed off the phone).
7. Field-scale sensing (RealSense, LiDAR, drones) as a separate data pipeline feeding the app.

**Note:** Windows Smart App Control on the development laptop blocks unsigned DLLs, so Python 3D libraries (Open3D, the RealSense SDK) will probably need WSL2 or Smart App Control turned off.
