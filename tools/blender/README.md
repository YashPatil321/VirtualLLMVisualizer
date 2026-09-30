# Rig part generator

`generate_rig_parts.py` builds low poly models of the rig's parts in Blender, at real
size, and exports them as FBX files that Unity picks up automatically.

Modelled on Rig 2 from photos: eight EVGA GTX 1070 SC cards on USB risers, standing in
a two level black aluminium frame, with two Antec HCP 1300 Platinum supplies at the ends
of the lower level.

| Part | Size (mm) | Triangles |
|---|---|---|
| `gpu_card` | 40 × 267 × 111, EVGA GTX 1070 SC: two fans, black backplate, lit logo, I/O bracket with a white dummy plug | 1,716 |
| `psu` | 200 × 150 × 86, Antec HCP 1300, lettered, with fan grille | 2,164 |
| `motherboard` | 305 × 244, ATX, with CPU, RAM and PCIe slots | 192 |
| `cpu_cooler` | 90 × 90 × 45, Intel stock cooler | 692 |
| `ram_stick` | 7 × 133 × 31 | 132 |
| `riser` | 40 × 100, x16 slot, USB 3 socket, 6 pin power | 132 |
| `frame` | 800 × 400 × 300, two levels | 216 |
| `pcie_cables` | 559 × 185 × 126, a pair of sleeved cables arching off each card | 2,304 |

The whole rig is about 26,000 triangles: cheap enough for a Quest 2 with every card on
screen. Real proportions, simple shapes; not final art.

## Run it

Install [Blender](https://www.blender.org/download/) (4.0 or newer). From the repo
folder in PowerShell:

```powershell
& "C:\Program Files\Blender Foundation\Blender 4.2\blender.exe" -b --factory-startup --python tools\blender\generate_rig_parts.py -- --out Assets\Art\Models
```

Change `Blender 4.2` to the version folder you installed. Add
`--preview rig_parts.png` to also render a picture of every part.

It prints one line per part and writes eight `.fbx` files to `Assets/Art/Models/`.

## Then in Unity

1. Switch back to Unity. It imports the models. `Assets/Editor/RigModelImportSettings.cs`
   sets their import options automatically, including lightmap UVs for baked lighting.
2. Run **OCS → Build Experience Scene**. The Console line should now say
   `23 parts from Assets/Art/Models` instead of `placeholder parts`: the frame,
   motherboard, cooler, RAM, cables and both supplies, plus eight risers and eight cards.
3. Commit the models. They go through Git LFS, so run `git lfs install` once on your
   machine first, then `git add Assets/Art/Models` and commit as usual. Include the
   `.meta` files Unity creates.

## What the scene builder relies on

- **Every part is centred on its origin**, so it drops in where the placeholders were.
- **Card children are named `Fan0`, `Fan1` and `LED`.** `CardVisual` spins the fans
  about their local X axis and drives the glow on every child whose name starts with
  `LED`: the light bar and EVGA lettering on the top edge. Rename them and the card goes
  still or dark.
- **Parts with a front carry an empty named `Front`** on that side: the card's bracket,
  the supply's lettering, the frame's bracket bar. The scene builder turns each part so
  its `Front` faces the viewer, so they come out the right way round whichever way the
  FBX axis conversion lands.
- **The card row is shared.** `CARD_SPACING` and the frame's `CARD_TIER` here must match
  `Assets/Data/RigLayout.asset` and `CardTier` in the scene builder. The script prints
  where the cable harness's centre sits from the middle of the card row; the builder's
  `HarnessFromCardRow` and `HarnessSize` hold those numbers. Change them together.
- **Axes:** the script builds with Blender's X across the card, Y along it and Z up. The
  exporter settings turn that into Unity's X, Z and Y, so parts arrive upright.

## How it was checked

Run headless with Blender 4.0.2 on Linux: all eight parts built and exported, and the
preview render was checked by eye. That check caught a frame whose rails overlapped at
the joints, which is now fixed. The Unity import side has not been checked yet, since
there was no Unity in that environment. If a part arrives rotated or at the wrong size,
the exporter settings in `export_fbx` are the place to look.

Ubuntu's own Blender package leaves out numpy, which the FBX exporter needs
(`apt-get install python3-numpy`). Official Blender downloads include it.
