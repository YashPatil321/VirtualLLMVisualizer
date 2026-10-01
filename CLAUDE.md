# CLAUDE.md

Context for Claude Code working in this repository. Read this before touching anything.

## What this is

A VR experience for Meta Quest that shows how our OCS LLM infrastructure works: the GPU rig assembling itself around the viewer, then a real inference request traveling through the system and landing on a specific graphics card in front of them.

This is an **experience**, not a tutorial. There is no quiz, no gating, no scoring. The viewer watches and looks around. Pacing and clarity matter more than interactivity.

Part of the CSH / OCS Intelligence Infrastructure project. Teammates Yash Parikh and Anvay Vahia own the broker and control plane. I own the GPU rigs and this VR layer.

How the system works, from the week 1 deck: a student's browser → AWS relay (EC2, NGINX + TLS) → FastAPI broker (auth, queue, scheduling; Redis for the live worker registry, RDS for users and history) → NetBird private overlay → llama-server on a GPU rig. Tokens stream back the same way. There are no Mac Minis. The model is Qwen3.8 27B, GGUF Q4_K_M, split across several 1070s per replica over PCIe (no NVLink). Rig 1 is production, Rig 2 is dev & test. About 12 tokens/s today, target 60+; warm start about 2 s, cold start 2 min 27 s. Live today the path is Open WebUI to Ollama over NetBird; the broker, Redis and RDS are being built. The VR shows the target path and says so.

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
- `Telemetry/PlaybackPace.cs` — gives every hop a minimum time on screen, so the network
  hops (milliseconds in reality) can be followed while the GPU part plays in real time.
  A hop can span several cards (`gpu_count`): a model replica

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
- `Experience/BeamGlowState.cs` — which system view links are lit, plain C#
- `Experience/SystemBeams.cs` — draws those links, and the beam from Rig 2 to the card
- `Experience/HopCaptions.cs` — what each node did and how long it took
- `Experience/TelemetryText.cs` — every readout string, plain C#
- `Experience/CardTelemetryLabel.cs` — load, temperature and tokens over the working card
- `Experience/TimelineLayout.cs`, `Experience/RequestTimeline.cs` — the "where the time
  goes" bar, with a playhead
- `Experience/PartLabel.cs` — names a part with a leader line as it seats. Text, offset
  and how long it stays come from the step's `label`, `labelOffset` and `labelSeconds`
- `Experience/FaceCamera.cs` — turns labels to face the viewer
- `Experience/StageMotion.cs` — how far a stage change is along, from the act, plain C#
- `Experience/StageDirector.cs` — sinks the empty tray and stand after assembly, grows
  the system view in at power on, brings the timeline in with the request
- `Experience/FlightPath.cs` — the arc, overshoot and spin parts fly on, plain C#
- `Experience/WorldPulse.cs` — the power on wave and the hall waking, plain C#
- `Experience/WorldPulseDriver.cs` — hands those, and the generation ripples, to the
  floor and rack shaders as global values once a frame
- `Experience/ImpactBursts.cs` — rings and sparks as parts seat, at power on, on the
  working card and at the client when the answer lands
- `Experience/TokenStream.cs` — tokens flying off the working card into the answer panel
- `Experience/AnswerText.cs`, `Experience/AnswerPanel.cs` — the prompt, and the answer
  typing itself out word by word
- `Experience/InfoBoard.cs` — a board of facts as data (`RigFacts.asset`), shown left of
  the viewer as "Rig 2 at a glance"
- `Experience/JourneyText.cs`, `Experience/JourneyBoard.cs` — "How a request flows", right
  of the viewer: every hop with its time, lit as the request reaches it, then the total
  and the GPU's share

Information lives in the data, so wording can be corrected without code: each assembly
step's `label` and `detail` (a fact line under the part's name), each system node's
`role` (what it does, on its panel), `RigFacts.asset`, and `prompt_preview` /
`response_preview` in the trace. Facts there are checked against spec sheets; anything
about how the scheduler assigns cards should only say what the trace shows.

Shaders, in `Assets/Shaders/`, all unlit and Single Pass Instanced safe
- `OCSGlow.shader` — additive soft light (blob, beam or ring): halos, beams, rings,
  sparks, tokens, dust. How the scene glows with no post processing
- `OCSGridFloor.shader` — the hall floor grid, the power on shockwave and the ripples
- `OCSRackLights.shader` — a whole rack face of blinking server lights on one quad

Editor
- `Editor/ExperienceSceneBuilder.cs` — OCS > Build Experience Scene. Uses models from
  `Assets/Art/Models` when present, placeholders otherwise. Uses
  `Assets/Prefabs/Environment.prefab` for the room when present, otherwise builds the
  data hall: rack rows, grid floor, dais, pillars, signs, dust and fog. OCS > Save Environment As Prefab saves the current room, so edits made to
  it by hand survive rebuilds. Edits to anything else in the scene do not
- `Editor/RigModelImportSettings.cs` — import settings for those models

Models: `tools/blender/generate_rig_parts.py` builds them in Blender at real size, modelled
on photos of Rig 2 (EVGA GTX 1070 SCs, a two level black frame, two Antec 1300 W supplies).
See `tools/blender/README.md`. Parts are centred on their origin. Card children named `Fan0`
and `Fan1` spin, and every child whose name starts with `LED` glows. An empty named `Front`
marks the side the builder turns toward the viewer. Card spacing and height are shared
between that script, `RigLayout.asset` and the builder; change them together.

Data, in `Assets/Data/`
- `RigLayout.asset`, `AssemblySequence.asset` (23 steps), `SystemGraph.asset`
  (8 nodes: student, AWS relay, broker, Redis, RDS, NetBird, Rig 2, Rig 1),
  `NarrationTrack.asset` (14 lines), `RigFacts.asset`, `sample-trace.json` (15 hops on the
  target path, the model on GPUs 0 to 3)

Tests in `Assets/Tests/EditMode/` cover the loader, card lighting, the assembly
timeline, hop routing, narration cues, beam glow, readout text, timeline layout,
stage timing, flight paths, the power on wave, the answer text, the journey board and
playback pacing.
`tools/headless-tests/` also compiles the shaders' HLSL with glslang when it is
installed; that catches syntax errors, not how they look.

Visuals subscribe to player events. Playback and state logic never touch Transforms
directly, which is why every one of those rules is covered by an edit mode test with no
scene. Keep that split.

## Running the code without Unity

`tools/headless-tests/run.sh` compiles everything against a `UnityEngine` shim and plays
the whole arc. It catches field name typos in `.asset` files, which are invisible in the
editor. It is a first pass, not a substitute for the Unity Test Runner, and it says
nothing about frame rate. See `tools/headless-tests/README.md`.
