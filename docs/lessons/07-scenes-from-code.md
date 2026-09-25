# Lesson 7: Building scenes from code

[← Lesson 6](06-connecting-ai-to-unity.md) · [Index](README.md) · Next: [Working with AI agents: what held up →](08-working-with-agents.md)

**You'll learn:** how to generate an entire Unity scene from data with one menu item,
why a scene built without eyes on it came out looking like a mess, and how to split
scene work between an agent that can't see and one that can.

**Needs:** the project open in Unity. About 30 minutes.

---

## Why build a scene from code at all

Wiring this scene by hand means creating dozens of GameObjects, adding six kinds of
component, and dragging eight cards, 23 parts and six nodes into the right inspector
slots. It's tedious, it's easy to get one slot wrong, and when the layout changes you
do it again.

An **editor script** can do all of it. `Assets/Editor/ExperienceSceneBuilder.cs` adds a
menu item:

```csharp
[MenuItem("OCS/Build Experience Scene")]
public static void BuildScene()
{
    RigLayout layout = Load<RigLayout>("RigLayout.asset");
    AssemblySequence sequence = Load<AssemblySequence>("AssemblySequence.asset");
    ...
}
```

Run **OCS → Build Experience Scene** and it:

1. loads the four data assets and the trace, and validates them first
2. creates a floor, and places the camera at roughly standing eye height
3. places eight cards using `RigLayout`, sized like a real GTX 1070
4. creates a tray, and one placeholder part per assembly step with rough real sizes
5. creates a marker and a text label for each node in `SystemGraph`
6. adds every component and connects every reference
7. saves the result as `Assets/Scenes/Experience.unity`

Running it again **rebuilds the scene from scratch**. That's deliberate. While the
layout is still being argued about, the scene is disposable. The real source of truth
is the script plus the data assets.

<details>
<summary><b>Predict first:</b> the narration uses Unity's old <code>TextMesh</code>, not TextMeshPro, which looks much better. Why?</summary>

TextMeshPro needs its "essentials" imported into the project before it renders
anything, and that's a manual step. A scene built by a script in a fresh project can't
assume that's been done. `TextMesh` works with no setup. It's a placeholder: at the
real art stage, `NarrationLabel` gets swapped for TextMeshPro, and nothing that feeds it
has to change.

</details>

## What it actually looked like

The builder ran first time, and the Console said:

```
[SceneBuilder] Built Assets/Scenes/Experience.unity: 8 cards, 23 assembly parts,
6 system nodes. Press Play to watch the arc on a flat screen.
```

The user's reaction to the scene view: **"what on earth is this bro"**.

A fair reaction. The system nodes were grey boxes hanging high in the sky. The tray
parts were piled on top of each other. The cards were a cluster of thin slabs off to
one side. Every object was present and wired correctly, and the whole thing read as
nothing.

## Why it looked like that

Every coordinate in the builder was chosen by the cloud agent, which had never seen a
single frame of the scene. It picked numbers that were plausible on paper: nodes at
1.4 to 2.2 metres high, the system view 1.5 metres behind the rig, tray parts 2 cm apart.
Without a way to look, there was no way to find out they were wrong.

That leads to the most useful split in the project:

| An agent that **can't see** is good at | An agent that **can see** is good at |
|---|---|
| Structure: what exists, what connects to what | Layout: where things go so they read well |
| Wiring every reference correctly | Checking the view from the camera |
| Making the build repeatable | Tuning numbers by looking and adjusting |
| Testing the logic | Catching "that looks wrong" |

The cloud agent built the structure. Tuning it is a job for the local agent connected
over MCP (lesson 6), or for a person.

## What you can judge on a monitor

Press **Play** and the whole five act arc runs on a normal screen. The built scene
shortens the quiet acts so it's quicker to watch. The data assets keep the real pacing.

| You can judge on a monitor | Only a headset tells you |
|---|---|
| Whether the acts happen in order | Whether 72 fps holds |
| Whether the right card lights up and stays lit | Whether the rig feels the right size standing next to it |
| Whether narration appears at the right moments | Whether text is readable at that distance |
| Whether the request path is followable | Whether anything causes discomfort |

This is the payoff from lesson 2. Because the logic doesn't depend on the headset,
almost everything except scale, comfort and frame rate can be checked before one is
plugged in.

## Handing the scene to an agent that can see

Two prompts, in order. First, confirm the foundations. This is still the most
important unverified step in the whole project:

> Read CLAUDE.md first. Then open the Test Runner (Window > General > Test Runner),
> switch to EditMode, and run all tests. Report exactly how many pass and fail, and
> paste the full failure output for any that fail. Don't fix anything yet.

Then fix the layout:

> The scene built by "OCS > Build Experience Scene" has bad placeholder positions. The
> system view nodes float too high above the rig and the tray parts are stacked on top
> of each other. Look at the scene through the Main Camera, then fix the coordinates in
> Assets/Editor/ExperienceSceneBuilder.cs so the whole thing reads properly from the
> viewer's position. Rebuild the scene, enter Play mode to check the five acts run,
> then commit and push.

Notice what these prompts do:

- **They say what to read first.** `CLAUDE.md` holds the rules.
- **They separate looking from fixing.** "Don't fix anything yet" gets you an honest
  report instead of a quiet patch.
- **They fix the source, not the output.** The layout gets fixed in the builder script,
  so the next rebuild doesn't undo it.
- **They say how to check.** Enter Play mode and watch the acts.

> **Try it**
>
> Run **OCS → Build Experience Scene**, then press Play. Write down three things that
> look wrong. Then compare your list with what the local agent finds when you give it
> the layout prompt.

## Checkpoint

- [ ] I've run **OCS → Build Experience Scene** and seen the Console message
- [ ] I've pressed Play and watched the five acts on a monitor
- [ ] I can explain why the first layout looked bad, and whose job fixing it is
- [ ] I've given the tests prompt to a connected agent and recorded the result

Next: [Working with AI agents: what held up →](08-working-with-agents.md)
