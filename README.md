# OCS Infrastructure VR

A standalone Meta Quest experience that shows what happens when you send a prompt to an
LLM: a GPU rig assembles itself around you, then a real inference request travels
through the system and lands on one graphics card in front of you.

Built with Unity 6 (URP, Android) and Claude Code.

## Start here

- **Want to build VR apps with Unity?** Read the beginner guide:
  [`docs/vr-guide/`](docs/vr-guide/README.md). Ten chapters, from installing Unity to
  running on a Quest, with grabbing, menus, locomotion, scripting, performance, and
  using an AI coding agent with the editor.
- **Working on this project?** Read [`CLAUDE.md`](CLAUDE.md), then
  [`docs/milestones.md`](docs/milestones.md).
- **Setting up a machine?** [`docs/setup-runbook.md`](docs/setup-runbook.md).

## Run the tests without Unity

```bash
sudo apt-get install -y mono-mcs
./tools/headless-tests/run.sh
```

A first pass only. The Unity Test Runner is the real check. See
[`tools/headless-tests/README.md`](tools/headless-tests/README.md).

## Status

Runtime code and data assets are written and pass 40 tests in the headless harness. The
project opens in Unity and builds its scene from code. Nothing has run on a headset
yet, so no milestone is ticked.
