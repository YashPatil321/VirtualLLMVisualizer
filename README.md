# OCS Infrastructure VR

A standalone Meta Quest experience that shows what happens when you send a prompt to an
LLM: a GPU rig assembles itself around you, then a real inference request travels
through the system and lands on one graphics card in front of you.

Built with Unity 6 (URP, Android) and Claude Code.

## Start here

- **Learning to build something like this?** Read the lesson series:
  [`docs/lessons/`](docs/lessons/README.md). Eight lessons covering architecture,
  testing Unity code without Unity, project hygiene, Windows setup, connecting an AI
  agent to the editor over MCP, and what worked and didn't.
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
