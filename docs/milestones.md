# Milestones

Work the lowest unfinished milestone. Do not start the next one until the current one runs on a headset. Check the box only when the acceptance criteria are met, not when the code compiles.

---

## V1: Toolchain

- [ ] Unity 6 LTS installed with the android module
- [ ] URP project created, git initialized with LFS and a Unity `.gitignore`
- [ ] `com.unity.pipeline` installed, `unity status` reports ready
- [ ] MCP server registered with Claude Code
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
- [ ] `data/sample-trace.json` loads and validates
- [ ] System view: nodes for client, broker, Mini, scheduler, both rigs
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
