# CLAUDE.md

Context for Claude Code working in this repository. Read this before touching anything.

## What this is

A VR experience for Meta Quest that shows how our OCS LLM infrastructure works: the GPU rig assembling itself around the viewer, then a real inference request traveling through the system and landing on a specific graphics card in front of them.

This is an **experience**, not a tutorial. There is no quiz, no gating, no scoring. The viewer watches and looks around. Pacing and clarity matter more than interactivity.

Part of the CSH / OCS Intelligence Infrastructure project. Teammates Yash Parikh and Anvay Vahia own the broker, control plane, and Mini worker layer. I own the GPU rigs and this VR layer.

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
docs/
data/
```

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

- `Assets/Scripts/Telemetry/TraceData.cs` — Trace and Hop types
- `Assets/Scripts/Telemetry/TraceLoader.cs` — parse, sort, validate
- `Assets/Scripts/Telemetry/TracePlayer.cs` — playback, raises HopStarted / HopEnded
- `Assets/Scripts/Rig/RigLayout.cs` — card slot positions as data
- `Assets/Scripts/Rig/RigCardDisplay.cs` — binds hops to card visuals
- `Assets/Scripts/Experience/ExperienceSequencer.cs` — the five act arc
- `Assets/Tests/EditMode/TraceLoaderTests.cs` — needs the Test Framework and an asmdef

Visuals subscribe to TracePlayer events. Playback logic never touches Transforms
directly. Keep that split: it is what lets the whole request path be tested in a flat
desktop scene before a headset is involved.
