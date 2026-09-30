# Milestones

Work the lowest unfinished milestone. Do not start the next one until the current one runs on a headset. Check the box only when the acceptance criteria are met, not when the code compiles.

---

## Status

No box below is checked, because nothing has run on a headset yet. What exists is the
code and data layer, written ahead of the toolchain so V1 is the only thing blocking.

Code complete and covered by tests, waiting on the editor:

| Milestone | What is written | What still needs Unity |
|---|---|---|
| V3 | `AssemblySequence.asset` holds the 23 step build order. `AssemblyTimeline` plays it. Parts fly in on arcs (`FlightPath`), spinning and trailing light, the lower level ones sliding in from the front, and land with a ring and sparks (`ImpactBursts`). `PartLabel` names each part, with a leader line, as it seats. | Part prefabs, tray and socket transforms, binding them in `AssemblyPlayer` |
| V4 | `CardVisual` spins fans and drives LED glow from load. `RigPower` powers cards on in a ripple at Act 3, then a shockwave runs out across the hall floor and wakes the racks as it passes (`WorldPulse`, the floor and rack shaders). | Audio cue, and judging whether it reads as a moment on a headset |
| V5 | `SystemGraph.asset` holds the five nodes. `SystemRouteState` routes hops to them. `SystemBeams` lights each link as the request crosses it, then a beam from Rig 2 to the working card. `HopCaptions`, `CardTelemetryLabel` and `RequestTimeline` say what each hop did, what the card is doing, and where the time went. While the card generates, tokens stream off it into `AnswerPanel`, which types the answer out, and ripples roll across the floor. | Node markers, the pulse, world space labels |
| V6 | `NarrationTrack.asset` holds 11 lines, plus one per assembly step. | A text component or AudioSource listening to `NarrationDirector` |

`tools/headless-tests/run.sh` compiles all of it and plays the full arc with no Unity
installed. Last run: 83 tests pass, all 23 assembly steps fire, card 3 holds full load
through generation, the hall wakes, every burst fires and the answer types out in full.
It also compiles the three shaders with glslang.

Those numbers are a simulation at a fixed 72 fps, not a measurement. Frame rate, draw
calls and anything about how it feels can only come from the device. The data hall is
the first thing to profile there: fog, about 170 racks (static batched), the rack light
shader across much of the view, and the additive glow quads' overdraw.

---

## V1: Toolchain

- [ ] Unity 6 LTS installed with the android module
- [ ] URP project created, git initialized with LFS and a Unity `.gitignore`
- [ ] `com.unity.pipeline` installed, `unity status` reports ready
- [ ] MCP server registered with Claude Code
- [ ] Test Framework package installed so the edit mode tests run
- [ ] OpenXR, XR Plugin Management, XR Interaction Toolkit installed via the Package Manager Client API
- [ ] Android build settings applied per `docs/setup-runbook.md`
- [ ] Empty scene builds and runs on the headset

**Done when:** you can put the headset on, look around a grey scene, and see your controllers. Committed.

---

## V2: Rig in the room

- [ ] Workbench scene with correct real world scale
- [ ] Rig blockout from ProBuilder primitives: chassis, board, 8 card slabs, PSU, risers
- [ ] Card slots laid out from a ScriptableObject, not hardcoded transforms
- [ ] Baked lighting, profiled on device

**Done when:** standing next to the rig in the headset feels like the right size, and frame rate is stable.

---

## V3: Assembly sequence

- [ ] Assembly step data format defined as a ScriptableObject
- [ ] Steps filled in from my Rig 2 build log
- [ ] Sequencer plays steps in order with per step timing from data
- [ ] Parts animate from tray to socket

**Done when:** the rig assembles itself start to finish, in the correct real order, with no scripted transforms hardcoded.

---

## V4: Power on

- [ ] Fan spin, card lighting, audio cue
- [ ] Transition from assembly into the powered state

**Done when:** the pile of parts becoming a machine reads as a moment, not a state change.

---

## V5: Trace playback

- [ ] Trace JSON deserialized into plain C# types per `docs/metrics-contract.md`
- [ ] `Assets/Data/sample-trace.json` loads and validates
- [ ] System view: nodes for client, broker, scheduler, both rigs
- [ ] Request travels the path with timing from the trace
- [ ] The named card lights and shows load and temperature climbing

**Done when:** the full request path plays from the sample trace with no network connection of any kind.

---

## V6: The whole arc

- [ ] All five acts sequenced end to end
- [ ] Narration, placeholder audio is fine
- [ ] Runs under five minutes unattended
- [ ] 72 fps held throughout, verified on device
- [ ] **Tested on three people outside the team**

**Done when:** all three can explain what the rigs do and where the model runs. If they cannot, fix the narration and retest. This is the milestone that decides whether the project worked.

---

## V7: Real art

- [ ] Blender models replacing placeholders
- [ ] Cable routing
- [ ] Normal maps baked, UVs packed into atlases
- [ ] Reprofiled on device

**Done when:** it looks like our actual rig and still holds 72 fps.

---

## V8: Live mode (optional)

- [ ] Metrics client hits the read only endpoint
- [ ] Live requests render on the same path as replayed ones
- [ ] Falls back to replay cleanly when the endpoint is unreachable

**Done when:** a prompt sent from a laptop appears in the headset within a second. Nice for a demo with the rigs running. Not required to ship.

---

## Dependency note

V1 through V7 depend only on me. V8 depends on Anvay's broker. That ordering is deliberate so nothing on my critical path waits on anyone else's sprint.
