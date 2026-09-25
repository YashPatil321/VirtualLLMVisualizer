# Chapter 4: Your first VR scene

[← Chapter 3](03-set-up-xr.md) · [Guide home](README.md) · Next: [Grabbing, buttons and menus →](05-interaction.md)

**Time:** 30 minutes. **Needs:** chapter 3 done. No headset needed.

You'll make a room with a floor and a table, put the player in it, and walk around it
using the keyboard and mouse simulator.

---

## Step 1: Make and save a new scene

1. **File → New Scene**, choose **Basic (URP)** or **Standard (URP)**, and create it.
2. **File → Save As**, make a folder `Assets/Scenes`, and save it as `VRRoom`.

Save often. Ctrl+S saves the scene.

## Step 2: Delete the normal camera

In the Hierarchy, right-click **Main Camera** and **Delete**.

<details>
<summary><b>Check yourself:</b> why delete it? It's just a camera.</summary>

The player rig you're about to add has its own camera, which follows the headset. With
two cameras, Unity might render the wrong one, and the one that doesn't follow your
head will make the whole world seem glued to your face. One camera, inside the XR
Origin.

</details>

## Step 3: Add the player

1. In the Project panel, open `Assets/Samples/XR Interaction Toolkit/<version>/Starter
   Assets/Prefabs`.
2. Drag **XR Interaction Setup** into the Hierarchy.
3. Select it and set its **Position** to `0, 0, 0` in the Inspector.

**What you just added:** an **XR Origin** (the player, with a head camera and two
controllers), an **XR Interaction Manager** (which coordinates every interaction), an
**Event System** set up for VR menus, and an **Input Action Manager** (which switches
the controller buttons on).

If your version has no XR Interaction Setup prefab, use **XR Origin (XR Rig)** instead,
then add **GameObject → XR → Interaction Manager** separately.

## Step 4: Check the tracking origin

Expand the setup and select **XR Origin**. In the Inspector, find **Tracking Origin
Mode** and set it to **Floor**.

**Why:** with **Floor**, the headset reports your real height above your real floor, so
a 1.7 m tall person sees the world from 1.7 m. With **Device**, the camera starts at the
height set in **Camera Y Offset** instead, and ignores how tall the person is. Floor is
right for standing experiences.

## Step 5: Add a floor

1. **GameObject → 3D Object → Plane**. Name it `Floor`.
2. Position `0, 0, 0`, Scale `2, 1, 2`.

A Unity plane is 10 × 10 units by default, so this makes a 20 × 20 metre floor. It
already has a **Mesh Collider**, which you'll need for teleporting in chapter 6.

## Step 6: Add a table, at real size

1. **GameObject → 3D Object → Cube**. Name it `Table`.
2. Scale `1.2, 0.75, 0.6`. That's 1.2 m wide, 75 cm tall, 60 cm deep, a normal table.
3. Position `0, 0.375, 1`.

**Why 0.375:** a cube's position is its centre. A 0.75 m tall cube with its centre at
0.375 m sits exactly on the floor. And **z = 1** puts it 1 metre in front of where the
player starts.

## Step 7: Give things colour

1. In the Project panel, right-click → **Create → Material**. Name it `Wood`.
2. With it selected, click the **Base Map** colour swatch and pick a brown.
3. Drag the material onto the table in the Scene view.

Make a second material for the floor so the table stands out against it.

## Step 8: Add the simulator

1. Open `Assets/Samples/XR Interaction Toolkit/<version>/XR Device Simulator`.
2. Drag the **XR Device Simulator** prefab into the Hierarchy.

## Step 9: Press Play

Press **Play**. The Game view shows what the player sees, and the simulator shows a
panel listing its keys. Read it once. It explains how to look around, move the headset,
switch between controlling the head and each hand, and press grip and trigger.

**You should see:** the table in front of you at about waist height, and two
controllers in the view that you can move with the simulator keys.

Press Play again to stop.

**Before building for a real headset,** delete or disable the XR Device Simulator in
your scene. Some XR Interaction Toolkit versions also have a project setting that adds
the simulator automatically in the editor only (look under **XR Plug-in Management →
XR Interaction Toolkit**), which saves you having to remember.

## Step 10: Add the scene to the build

**File → Build Profiles**, then in the **Scene List** click **Add Open Scenes**. Only
scenes in this list end up in the app.

> **Try it:** add a cup on the table. A cup is about 8 cm across and 10 cm tall, so try
> a Cylinder with scale `0.08, 0.05, 0.08` (a cylinder is 2 units tall, so 0.05 gives
> 10 cm), placed at y = 0.8. Press Play and check it looks cup-sized next to the table.

## If something's wrong

| Problem | Fix |
|---|---|
| The view is stuck, or glued to your face | There's still a second camera in the scene. Delete it |
| You're underground or floating | The XR Origin isn't at y = 0, or Tracking Origin Mode isn't Floor |
| Controllers don't appear | Check the XR Device Simulator is in the scene, and that Starter Assets were imported |
| Everything is pink | A material uses a non-URP shader. Select it and set its Shader to **Universal Render Pipeline/Lit** |
| Changes disappeared | You edited in Play mode. Stop, then edit |

## Checkpoint

- [ ] My scene has one camera, inside the XR Origin
- [ ] Tracking Origin Mode is Floor, and the origin is at 0, 0, 0
- [ ] The floor and table are at real size, and look right in the simulator
- [ ] The scene is in the Build Profiles scene list

Next: [Grabbing, buttons and menus →](05-interaction.md)
