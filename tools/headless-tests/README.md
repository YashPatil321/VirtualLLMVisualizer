# Headless tests

Compiles and runs the project's C# with no Unity installed. Useful for a fast check
before opening the editor, and for CI.

```bash
sudo apt-get install -y mono-mcs      # once
sudo apt-get install -y glslang-tools # once, optional: also checks the shaders
./tools/headless-tests/run.sh
```

Four things run:

| | What it checks |
|---|---|
| `RunTests` | The real edit mode tests in `Assets/Tests/EditMode/`, unmodified |
| `FullArc` | Loads the real `.asset` files and plays all five acts at 72 fps |
| `Sim` | The request act alone, frame by frame, printing card 3's load |
| `check_shaders.py` | Compiles the HLSL in `Assets/Shaders/` with glslang, fog on and off, against stand-ins for the URP functions they call. Syntax and types only |

`FullArc` also checks the hall reacts: the power on wave runs and wakes the racks, the
floor ripples while the card generates, every burst fires, tokens stream only during
generation, and the answer types out to its last word.

`FullArc` is the one that catches the expensive mistakes: it binds every key in every
`.asset` to a real C# field and reports any that do not match. A typo'd field name in a
`.asset` is invisible in Unity, where the field just silently keeps its default.

## What this is not

`UnityShim.cs` is a stand-in for `UnityEngine`, and its `JsonUtility` is a
reimplementation, not Unity's. It follows the documented behaviour that the loader
depends on — public fields only, literal key to field name mapping, unknown keys
ignored, absent keys left at their initializer — but it can drift.

So this suite is a fast first pass, not a replacement for the Unity Test Runner. Any
result that matters still gets confirmed in the editor, and anything involving
rendering, XR, or frame rate can only be measured on the headset.
