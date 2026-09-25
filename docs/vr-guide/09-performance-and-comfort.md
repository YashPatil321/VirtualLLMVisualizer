# Chapter 9: Performance and comfort

[← Chapter 8](08-build-to-quest.md) · [Guide home](README.md) · Next: [Using an AI coding agent with Unity →](10-ai-agents-and-unity.md)

**Time:** 1 hour. **Needs:** a working build from chapter 8.

A VR app that stutters or makes people dizzy fails, however good it looks. This chapter
covers how to keep your frame rate steady on a standalone headset, how to measure it,
and what else makes an app comfortable.

---

## The frame budget

Every frame has to be finished in time for the display:

| Refresh rate | Time per frame |
|---|---|
| 72 Hz (Quest 2 default) | 13.9 ms |
| 90 Hz | 11.1 ms |
| 120 Hz | 8.3 ms |

That time is split between the **CPU** (your scripts, physics, and preparing what to
draw) and the **GPU** (actually drawing it, twice). Whichever takes longer is your
limit. You'll find out which with the tools at the end of this chapter.

**Measure on the headset, always.** The editor and Link run on your PC and tell you
almost nothing about how the headset copes.

## The biggest wins, roughly in order

### 1. Bake your lighting

Real-time lights and shadows are expensive, and you pay for them every frame. For
anything that doesn't move, **bake** the lighting once, in the editor, into textures
called lightmaps.

1. Select your non-moving objects (floor, walls, table) and tick **Static** at the top
   right of the Inspector.
2. Set your lights' **Mode** to **Baked** (or **Mixed** if moving objects need to be lit
   by them too).
3. **Window → Rendering → Lighting → Generate Lighting.**

You get soft, good-looking lighting for almost no cost at runtime.

### 2. Keep draw calls down

A **draw call** is the CPU telling the GPU "draw this object with this material". Each
one costs CPU time, and in VR everything is drawn for two eyes. Fewer, larger batches
are better.

- **Reuse materials.** Ten objects sharing one material batch together far better than
  ten objects with ten slightly different materials.
- **Mark non-moving objects Static**, which lets Unity combine them.
- **For many copies of the same thing** (trees, chairs, bricks), tick **Enable GPU
  Instancing** on their material.
- **See your draw calls:** **Window → Analysis → Frame Debugger** lists every draw in a
  frame.

There's no single magic number. Meta's guidance for Quest 2 is in the low hundreds of
draw calls per frame, but measure your own app rather than trusting a target.

### 3. Set up URP for mobile VR

Find the URP asset your Android build uses: **Project Settings → Quality**, check the
**Render Pipeline Asset** for the level Android uses, and select that asset.

| Setting | Recommended | Why |
|---|---|---|
| **HDR** | Off | Expensive on mobile GPUs, rarely needed |
| **Anti-aliasing (MSAA)** | 4x | Jagged edges are very visible in VR, and MSAA is cheap on the Quest's GPU |
| **Render Scale** | 1.0 | Lower it only if you're GPU bound and out of other options |
| **Main light shadows** | Off, or short distance and low resolution | Real-time shadows are one of the biggest costs |
| **Additional lights** | Off or per-vertex | Each real-time light adds cost |
| **Post-processing** | Off unless measured | Bloom, colour grading and similar are expensive on mobile |
| **Depth / Opaque texture** | Off unless an effect needs it | Extra full-screen copies each frame |

### 4. Watch transparency

Transparent and see-through things (glass, particles, fog, UI with lots of layers) make
the GPU draw the same pixels many times over. This is called **overdraw**. Use it
sparingly, and avoid large transparent areas covering the view.

### 5. Keep models and textures reasonable

- Use simple models, especially for things far away. **LOD Groups** swap in simpler
  versions at a distance.
- Keep textures only as big as they need to be (1024 or 2048 pixels is plenty for most
  things), compressed as **ASTC**, with **mipmaps** on.

### 6. Scripts

From chapter 7: no allocations, `GetComponent` or `Find` in `Update`, and events
instead of checking every frame.

<details>
<summary><b>Check yourself:</b> your app drops frames only when you look at a big glass window with rain particles behind it. CPU or GPU problem, most likely? Why?</summary>

GPU. Transparency and particles cause overdraw: the GPU draws the same pixels over and
over. The fact it depends on where you're looking is a strong hint, since the CPU work
doesn't change much with view direction, but the pixels drawn do.

</details>

## Measuring on the headset

### See the frame rate: OVR Metrics Tool

Meta's **OVR Metrics Tool** app shows a small overlay in the headset with your frame
rate and CPU and GPU load, while your app runs. Install it from Meta's developer site
or store and turn on its overlay. It's the quickest way to see whether you're hitting
72.

### Find out why: the Unity Profiler

1. In **Build Profiles**, tick **Development Build** and **Autoconnect Profiler**.
2. **Build And Run** with the headset connected by USB.
3. **Window → Analysis → Profiler** in Unity. It shows what's taking the time, frame by
   frame, from the actual headset.

Turn **Development Build** off for the version you share with people. It runs slower.

## Comfort beyond frame rate

Chapter 6 covered locomotion comfort. A few more rules:

- **Real scale.** 1 unit is 1 metre. People notice immediately when a door is too tall
  or a table too low.
- **Readable text.** Text in VR needs to be much bigger than on a monitor. Place panels
  1 to 2 metres away, and if you have to squint, it's too small.
- **Don't put things too close.** Objects closer than about 0.5 m are hard to focus on
  for long. Brief is fine, like a held object. A menu glued to the face isn't.
- **No flashing.** Avoid strobing lights and sudden full-screen brightness changes.
- **Spatial sound.** On an **Audio Source**, set **Spatial Blend** to **3D** so sound
  comes from where the object is. It helps people know where to look.
- **Support different bodies.** Test seated and standing, and make sure important
  things aren't out of reach for shorter or taller people.

## Before you share your app

- [ ] Holds the target frame rate on the headset, in the busiest part of the app
- [ ] Lighting baked for everything static
- [ ] No real-time shadows or post-processing you haven't measured
- [ ] Teleport and snap turn are the defaults, with comfort options
- [ ] Text readable at its distance
- [ ] Tested by at least one person who isn't you, ideally someone new to VR
- [ ] Development Build turned off

## Checkpoint

- [ ] I know my frame budget and whether I'm CPU or GPU bound
- [ ] I've baked lighting and checked draw calls in the Frame Debugger
- [ ] My URP asset is set up for mobile VR
- [ ] I've measured my app on the headset with OVR Metrics Tool or the Profiler

Next: [Using an AI coding agent with Unity →](10-ai-agents-and-unity.md)
