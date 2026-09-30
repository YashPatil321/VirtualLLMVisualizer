# CLAUDE.md

Context for Claude Code working in this repository. Read this before touching anything.

## What this is

A VR experience for Meta Quest that shows how our OCS LLM infrastructure works: the GPU rig assembling itself around the viewer, then a real inference request traveling through the system and landing on a specific graphics card in front of them.

This is an **experience**, not a tutorial. There is no quiz, no gating, no scoring. The viewer watches and looks around. Pacing and clarity matter more than interactivity.

Part of the CSH / OCS Intelligence Infrastructure project. Teammates Yash Parikh and Anvay Vahia own the broker and control plane. There are no Mac Minis in the path: the scheduler sends requests straight to a GPU rig. I own the GPU rigs and this VR layer.

## Hard constraints

These are not preferences. Violating them breaks the build or the experience.

- **Engine:** Unity 6 LTS, Universal Render Pipeline. Not built in RP.
- **Target:** Android, ARM64, IL2CPP, standalone on device. Not PC tethered.
- **Minimum device:** Quest 2. Quest 3 is nicer but must not be required.
- **Frame rate:** 72 fps floor. A dropped frame is a bug, not a polish item.
- **Stereo:** Single Pass Instanced.
- **Networking:** the headset reads one read only metrics endpoint. It never joins NetBird, never connects to a rig directly, and never holds credentials.
- **Runtime:** must run end to end in replay mode with no infrastructure available.

## Why Unity and not Unreal

Decided and closed. Nanite and Lumen do not run on the mobile renderer, so Unreal's visual advantage is switched off on a standalone headset, and its cook and package loop is much slower. Do not reopen this unless the target changes to PC tethered.

## Repository layout

```
Assets/
  Scripts/
    Experience/     sequencing, timeline control
    Telemetry/      metrics client, trace parsing, replay
    Rig/            rig assembly, per card visual state
  Prefabs/
  Scenes/
  Art/
  Data/            trace JSON, imported by Unity as TextAsset
  Tests/
docs/
```

`Assets/Data/` rather than a top level `data/`: TracePlayer takes a `TextAsset`, and
Unity only imports assets under `Assets/`. A trace outside it cannot be assigned in the
inspector or shipped in the build.

## Conventions

- C#, namespace `OCS.VR`
- One MonoBehaviour per file, file name matches the class
- ScriptableObjects for anything data shaped (assembly steps, timing, card layout). No sequences hardcoded in scripts.
- Telemetry types are plain C# classes deserialized from JSON, not MonoBehaviours
- No allocation in Update. This is a mobile target and GC spikes read as stutter in a headset.

## Working with the Unity editor

Run `unity status` before editing any scene, GameObject, prefab, or asset. If an editor is connected and ready, drive it with live commands instead of editing project files by hand. The editor keeps its in memory state in sync. Hand editing a scene file behind a running editor loses work.

If `unity status` times out, check Safe Mode first. A C# compile error boots the editor into Safe Mode where the pipeline package does not load and every command fails with nothing useful. Run `unity pipeline list` to confirm, fix the compile errors, restart Unity. Do not fall back to blind file editing.

## Do not

- Hand edit `manifest.json`. Use the Package Manager Client API.
- Commit `Library/`, `Temp/`, `obj/`, or `Build/`.
- Commit a script or asset without its `.meta` file.
- Add realtime shadows, post processing stacks, or dynamic lights without profiling on device first.
- Build live metrics mode before replay mode works.
- Put anything on the critical path that depends on the broker existing.

## Current state

See `docs/milestones.md` for where we are. Work the lowest unfinished milestone. Each one has acceptance criteria. Do not start the next one until the current one is demoable on a headset.

## Code already written

The telemetry and sequencing layer exists and needs no XR packages, so it compiles
in a plain Unity project before any plugin work.

Telemetry
- `Telemetry/TraceData.cs` — Trace and Hop types
- `Telemetry/TraceLoader.cs` — parse, sort, validate
- `Telemetry/TracePlayer.cs` — playback, raises HopStarted / HopEnded

Rig
- `Rig/RigLayout.cs` — card slot positions as data
- `Rig/CardLoadState.cs` — per card load, plain C#, no Transforms
- `Rig/RigCardDisplay.cs` — turns that state into card visuals
- `Rig/AssemblyStep.cs`, `Rig/AssemblySequence.cs` — the build order as data
- `Rig/AssemblyTimeline.cs` — which step is running, plain C#
- `Rig/CardVisualMath.cs` — fan speed and LED glow for a load, plain C#
- `Rig/CardVisual.cs` — spins a card's fans and drives its LED

Experience
- `Experience/ExperienceSequencer.cs` — the five act arc
- `Experience/AssemblyPlayer.cs` — moves parts from tray to socket
- `Experience/SystemGraph.cs` — system view topology as data
- `Experience/SystemRouteState.cs` — where the request is, plain C#
- `Experience/SystemViewDisplay.cs` — draws nodes and the travelling pulse
- `Experience/NarrationTrack.cs` — narration lines as data, keyed by cue
- `Experience/NarrationDirector.cs` — picks the line for what is happening
- `Experience/RigPower.cs` — powers the cards on in a ripple at Act 3
- `Experience/RoomAtmosphere.cs` — ambient light and background, carried by the room

Editor
- `Editor/ExperienceSceneBuilder.cs` — OCS > Build Experience Scene. Uses models from
  `Assets/Art/Models` when present, placeholders otherwise. Uses
  `Assets/Prefabs/Environment.prefab` for the room when present, a default dark room
  otherwise. OCS > Save Environment As Prefab saves the current room, so edits made to
  it by hand survive rebuilds. Edits to anything else in the scene do not
- `Editor/RigModelImportSettings.cs` — import settings for those models

Models: `tools/blender/generate_rig_parts.py` builds them in Blender at real size. See
`tools/blender/README.md`. Parts are centred on their origin, and card children must be
named `Fan0`, `Fan1` and `LED`.

Data, in `Assets/Data/`
- `RigLayout.asset`, `AssemblySequence.asset` (23 steps), `SystemGraph.asset`
  (5 nodes), `NarrationTrack.asset` (11 lines), `sample-trace.json`

Tests in `Assets/Tests/EditMode/` cover the loader, card lighting, the assembly
timeline, hop routing and narration cues.

Visuals subscribe to player events. Playback and state logic never touch Transforms
directly, which is why every one of those rules is covered by an edit mode test with no
scene. Keep that split.

## Running the code without Unity

`tools/headless-tests/run.sh` compiles everything against a `UnityEngine` shim and plays
the whole arc. It catches field name typos in `.asset` files, which are invisible in the
editor. It is a first pass, not a substitute for the Unity Test Runner, and it says
nothing about frame rate. See `tools/headless-tests/README.md`.
