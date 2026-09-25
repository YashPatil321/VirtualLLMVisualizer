# Rig part generator

`generate_rig_parts.py` builds low poly models of the rig's parts in Blender, at real
size, and exports them as FBX files that Unity picks up automatically.

| Part | Size (mm) | Triangles |
|---|---|---|
| `gpu_card` | 40 × 267 × 111, dual fan | 1,060 |
| `psu` | 150 × 160 × 86, with fan grille | 584 |
| `motherboard` | 305 × 244, ATX, with CPU, RAM and PCIe slots | 168 |
| `riser` | 40 × 100, with an x16 slot | 36 |
| `frame` | 720 × 400 × 300, open air | 132 |

Cheap enough for a Quest 2 even with all eight cards on screen. These are placeholders
with real proportions, not final art.

## Run it

Install [Blender](https://www.blender.org/download/) (4.0 or newer). From the repo
folder in PowerShell:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe" -b --factory-startup --python tools\blender\generate_rig_parts.py -- --out Assets\Art\Models
```

Change `Blender 4.2` to the version folder you installed. Add
`--preview rig_parts.png` to also render a picture of every part.

It prints one line per part and writes five `.fbx` files to `Assets/Art/Models/`.

## Then in Unity

1. Switch back to Unity. It imports the models. `Assets/Editor/RigModelImportSettings.cs`
   sets their import options automatically, including lightmap UVs for baked lighting.
2. Run **OCS → Build Experience Scene**. The Console line should now say
   `19 parts from Assets/Art/Models` instead of `placeholder parts`: the frame,
   motherboard and PSU, plus eight risers and eight cards.
3. Commit the models. They go through Git LFS, so run `git lfs install` once on your
   machine first, then `git add Assets/Art/Models` and commit as usual. Include the
   `.meta` files Unity creates.

## What the scene builder relies on

- **Every part is centred on its origin**, so it drops in where the placeholders were.
- **Card children are named `Fan0`, `Fan1` and `LED`.** `CardVisual` spins the fans about
  their local X axis and drives the LED's glow. Rename them and the card goes still.
- **Axes:** the script builds with Blender's X across the card, Y along it and Z up. The
  exporter settings turn that into Unity's X, Z and Y, so parts arrive upright with no
  rotation.

## How it was checked

Run headless with Blender 4.0.2 on Linux: all five parts built and exported, and the
preview render was checked by eye. That check caught a frame whose rails overlapped at
the joints, which is now fixed. The Unity import side has not been checked yet, since
there was no Unity in that environment. If a part arrives rotated or at the wrong size,
the exporter settings in `export_fbx` are the place to look.

Ubuntu's own Blender package leaves out numpy, which the FBX exporter needs
(`apt-get install python3-numpy`). Official Blender downloads include it.
