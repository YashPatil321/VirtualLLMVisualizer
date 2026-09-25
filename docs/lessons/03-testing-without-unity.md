# Lesson 3: Testing Unity code when you don't have Unity

[← Lesson 2](02-architecture.md) · [Index](README.md) · Next: [Unity project hygiene →](04-project-hygiene.md)

**You'll learn:** how to compile and run Unity C# with no Unity installed, how three
real bugs were found that way, and exactly what this kind of testing can and can't
prove.

**Needs:** optional. To run the harness yourself you need Linux or WSL with
`mono-mcs`. About 40 minutes.

---

## The situation

The AI agent was working in a cloud container: Linux, no Unity, no headset, no display.
The project's C# had been written but never compiled. The only checks available were
"the braces balance" and "the JSON parses", which prove almost nothing.

The first attempt to install the .NET SDK failed: the container's network proxy blocked
Microsoft's download CDN. The fallback worked:

```bash
apt-get install -y mono-mcs
```

That's the Mono C# compiler. It can compile C#, but the project's code says
`using UnityEngine;` everywhere, and there's no UnityEngine to link against.

## The shim

The answer is a **shim**: a small fake `UnityEngine` that provides just enough of the
real API for the project to compile and run. `tools/headless-tests/UnityShim.cs` has
`Vector3`, `Mathf`, `Debug`, `Time`, `MonoBehaviour`, `ScriptableObject`, `TextAsset`
and a handful more, each a few lines long.

The one that needed care was `JsonUtility`, because the trace loader depends on its
exact behaviour. The shim reimplements the documented rules:

- only public fields are filled
- a JSON key maps to a field of exactly the same name
- unknown keys are ignored
- missing keys leave the field at its initialised value

It also includes a tiny NUnit replacement, so the project's real test files in
`Assets/Tests/EditMode/` compile and run **unmodified**.

```bash
./tools/headless-tests/run.sh
```

That compiles the editor scripts, runs every edit mode test, loads the real `.asset`
files and plays all five acts at a simulated 72 fps.

## Tests pass. Now simulate.

The original seven tests all passed on the first run. That's worth something, but it
only proves the loader works. The real question is what the viewer sees. So the harness
also **simulates** the experience: it creates the components, calls their `Awake`,
`Start` and `Update` methods in the order Unity would, advances time one 72 fps frame
at a time, and records what happens.

That's where the bugs were.

## Bug 1: the card that never lit up

Here's part of the sample trace. Card 3 is where the model runs.

```json
{ "hop": "rig",   "t_start_ms": 141, "t_end_ms": 168,  "gpu_index": 3 },
{ "hop": "model", "t_start_ms": 168, "t_end_ms": 3240, "gpu_index": 3 }
```

And here's the order `TracePlayer.Update` does things in each frame:

```csharp
// Start any hop whose window has opened.
while (_nextIndex < _hops.Count && _hops[_nextIndex].t_start_ms <= _elapsedMs)
    { ... HopStarted?.Invoke(h); ... }

// End any active hop whose window has closed.
for (int i = _active.Count - 1; i >= 0; i--)
    if (_active[i].t_end_ms <= _elapsedMs) { ... HopEnded?.Invoke(h); }
```

The original card display set a card's target load on `HopStarted` and set it to zero
on `HopEnded`.

<details>
<summary><b>Predict first:</b> what does card 3 do during the three seconds the model is generating?</summary>

It goes dark, and stays dark.

At the frame where elapsed time passes 168 ms, both things happen in the same
`Update`. Starts run first, so the model hop starts and card 3's target goes to full.
Then ends run, the rig hop's window has closed, and its `HopEnded` sets card 3's target
to zero. Nothing ever sets it back. Card 3 sits dark for the whole of generation, which
is the one moment the experience exists to show.

The simulation measured it: peak load on card 3 during generation was **0.18**,
falling to **0.00** within a frame.

</details>

**The fix** was to count how many hops are live on each card, and only go dark when
the count reaches zero:

```csharp
public void HopEnded(int card)
{
    if (!IsValidIndex(card)) return;

    if (_activeHops[card] > 0) _activeHops[card]--;

    // Still busy with an overlapping hop, so leave it lit.
    if (_activeHops[card] > 0) return;

    _target[card] = 0f;
    if (ActiveCard == card) ActiveCard = -1;
}
```

After the fix, card 3 holds **1.00** through generation. The regression test is
`AdjacentHopsOnOneCardDoNotBlankIt` in `CardLoadStateTests.cs`.

## Bug 2: the act that lasted zero seconds

`TracePlayer` has a `playOnStart` field that defaults to `true`. `ExperienceSequencer`
also calls `tracePlayer.Play()` when Act 4 begins, then waits for a `_traceDone` flag
set by the `TraceFinished` event.

<details>
<summary><b>Predict first:</b> with both in the scene, how long does Act 4 last?</summary>

Zero seconds.

The trace starts playing on its own at startup, during Act 1. It finishes about eight
seconds in and raises `TraceFinished`, which sets `_traceDone`. Act 4 comes around two
minutes later, calls `Play()`, then checks `_traceDone`. It's already true, so the act
ends immediately.

The simulation logged Act 4 and Act 5 starting at the same instant: **t = 125.0 s** for
both.

</details>

**The fix** had two parts. The sequencer takes ownership of the player in `Awake`:

```csharp
void Awake()
{
    // Awake, not Start: component Start order is undefined and TracePlayer.Start
    // would win.
    if (tracePlayer != null) tracePlayer.playOnStart = false;
}
```

And the flag is cleared right before `Play()`, not at the top of the arc, so nothing
from an earlier act can satisfy the wait. Act 4 now lasts **9.5 s**: the 3.3 s trace
at half speed, plus the tail hold.

## Bug 3: the part that jumped back to the tray

This one was introduced by the agent while writing new code, and caught by writing its
test. Between assembly steps there's a short gap. During the gap, `AssemblyTimeline`
reported travel progress `0`. `AssemblyPlayer` uses that number to place the part
between tray and socket, so every part that had just seated would snap back to the tray
for a quarter of a second.

The test `PartStaysSeatedDuringTheGapBetweenSteps` failed first, then passed after the
fix. That's the order you want.

> **Where it went wrong**
>
> The harness itself had bugs too. It's code, so of course it did:
>
> - The shim was missing `GetComponent`. The compiler said so; easy.
> - The shim's `GameObject` created a `Transform`, whose constructor created a
>   `GameObject`, whose constructor created a `Transform`... The run died with a stack
>   overflow.
> - The agent fixed the shim in its scratch folder but not the copy in `tools/` that
>   `run.sh` actually uses, and was briefly confused why the fix did nothing.
>
> None of these were project bugs, but each looked like one at first. When a test
> harness fails, check the harness before the code.

## Checking the data files too

The `.asset` files in `Assets/Data/` are YAML that Unity reads into ScriptableObjects by
matching field names. A typo like `travelSecond` instead of `travelSeconds` isn't an
error in Unity. The field just keeps its default value, silently.

So the harness includes a small loader that reads each `.asset` and binds every key to
the real C# type by reflection. Any key that doesn't match a real field is reported.
It's one of the more useful checks in the suite, because the editor won't tell you.

## What this proves, and what it doesn't

| The harness can tell you | The harness cannot tell you |
|---|---|
| The code compiles | Whether it compiles under Unity's real compiler and packages |
| The logic does what the tests say | Whether Unity's real `JsonUtility` behaves like the shim's |
| Events fire in the right order, at simulated 72 fps | Whether the headset actually holds 72 fps |
| Every asset key matches a real field | Whether the scene looks right |
| The full arc completes in 114 s | Whether it feels right to stand in |

It's a fast first pass. The real Unity Test Runner still has to confirm the same 40
tests, and at the time of writing it hasn't yet.

> **Try it**
>
> On Linux or WSL:
>
> ```bash
> sudo apt-get install -y mono-mcs
> ./tools/headless-tests/run.sh
> ```
>
> Then break something on purpose. In `CardLoadState.HopEnded`, delete the line
> `if (_activeHops[card] > 0) return;` and rerun. You should see:
>
> ```
>   FAIL  AdjacentHopsOnOneCardDoNotBlankIt :: expected <1> but was <0>. card 3 must stay lit through generation
> 39 passed, 1 failed
> ```
>
> The script stops there, because a failed test stage stops the run. That's bug 1,
> caught by its own regression test. Put the line back and rerun to see 40 pass.

## Checkpoint

- [ ] I can explain what a shim is and why `JsonUtility` needed the most care
- [ ] I can explain bug 1 without looking: why the card went dark
- [ ] I can explain bug 2: why the act collapsed
- [ ] I know three things the headless harness cannot prove
- [ ] (optional) I've run the harness and made a test fail on purpose

Next: [Unity project hygiene that saves you later →](04-project-hygiene.md)
