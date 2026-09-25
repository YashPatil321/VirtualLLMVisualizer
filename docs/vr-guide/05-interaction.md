# Chapter 5: Grabbing, buttons and menus

[← Chapter 4](04-first-vr-scene.md) · [Guide home](README.md) · Next: [Moving around →](06-locomotion.md)

**Time:** 45 minutes. **Needs:** the scene from chapter 4. No headset needed.

This is where VR starts to feel like VR. You'll make objects you can pick up and throw,
a button you can press, a slot that things snap into, and a floating menu. For most of
it you won't write any code.

---

## How the controllers map to interactions

The Starter Assets rig uses these controls by default:

| Controller | XR Interaction Toolkit calls it | Typical use |
|---|---|---|
| **Grip** (middle finger button) | **Select** | Grab and hold |
| **Trigger** (index finger) | **Activate**, or select for menus | Use a held object, click a button on a menu |
| Pointing at something | **Hover** | Highlight what you're about to grab |

The rig lets you grab both **up close** (reach out and grab) and **from a distance**
(point the ray and grip). Both work with everything in this chapter.

## Part A: Something you can pick up

### Step 1: Make the object

1. **GameObject → 3D Object → Cube**. Name it `Block`.
2. Scale `0.1, 0.1, 0.1` (10 cm). Position `0, 0.8, 1`, just above the table.

### Step 2: Make it grabbable

With `Block` selected, click **Add Component** and add **XR Grab Interactable**.

**You should see:** a **Rigidbody** component appear automatically as well. A
Rigidbody makes the object obey physics: gravity, falling, being thrown.

### Step 3: Try it

Press Play. Point a controller at the block (or reach for it) and press the simulator's
grip key.

**You should see:** the block lifts into your hand, follows it, and drops or flies off
when you let go.

<details>
<summary><b>Check yourself:</b> when you press Play, the block falls straight through the table. Why?</summary>

Physics only collides objects that both have **colliders**. Cubes get a Box Collider
automatically, so check the table didn't lose its collider. If you replaced the table
with an imported 3D model, add a **Box Collider** or **Mesh Collider** to it.

</details>

### Step 4: Choose how it follows your hand

On the XR Grab Interactable, **Movement Type** has three options:

| Movement Type | Feels like | Use for |
|---|---|---|
| **Instantaneous** | Glued exactly to your hand, no lag | Tools, small props |
| **Kinematic** | Smooth, still collides with other things | Most objects |
| **Velocity Tracking** | Heavy and physical; bumps into things instead of passing through them | Heavy objects, anything that should feel real |

**Throw On Detach**, ticked by default, lets you throw things. Untick it for things you
want to just drop.

### Step 5: Set where the hand holds it

By default you grab the object wherever you touched it. For something like a hammer,
you want the hand on the handle every time.

1. Right-click `Block` → **Create Empty**. Name it `GripPoint`.
2. Move `GripPoint` to where the hand should hold.
3. On the XR Grab Interactable, drag `GripPoint` into **Attach Transform**.

## Part B: A button

### Step 1: Make the button

1. Create a **Cylinder**, name it `Button`, scale `0.08, 0.01, 0.08`, and place it on
   the table (y = 0.76).
2. Give it a red material.
3. Add the component **XR Simple Interactable**. It's an interactable you can hover and
   select but not pick up.

### Step 2: Make it do something, with no code

1. Create another cube somewhere in the room, name it `Surprise`, and **untick the box
   next to its name** in the Inspector to hide it.
2. Select `Button`. On XR Simple Interactable, expand **Interactable Events**.
3. Under **Select Entered**, click **+**.
4. Drag `Surprise` from the Hierarchy into the empty object slot.
5. In the function dropdown, choose **GameObject → SetActive (bool)**, and tick the box.

Press Play and select the button.

**You should see:** the hidden cube appears.

**Why events:** Select Entered is a list of things to do when the button is selected.
You can add as many as you like: play a sound, show an object, start an animation. In
chapter 7 you'll call your own scripts from here too.

## Part C: A slot that things snap into

Sockets are great for "put the key in the lock" or "put the battery in the torch".

1. Create an empty GameObject, name it `Slot`, and place it on the table.
2. Add a **Sphere Collider**, tick **Is Trigger**, and set **Radius** to `0.08`.
3. Add **XR Socket Interactor**.

Press Play, pick up the block, and let go of it near the slot.

**You should see:** the block snaps into the slot and stays there. Grab it again to
take it out.

**Why Is Trigger:** a trigger collider detects things entering it without physically
blocking them. The socket uses it to notice objects nearby.

## Part D: A floating menu

VR menus live in the world, as flat panels you point at. Unity calls them **world
space** canvases.

### Step 1: Create the canvas

**GameObject → XR → UI Canvas.**

**Why this menu item, not the normal UI → Canvas:** the XR version adds a **Tracked
Device Graphic Raycaster**, which lets the controller rays click things. A normal canvas
only responds to a mouse.

### Step 2: Size and place it

Canvases are measured in pixels, so you shrink them down to metres:

1. On the canvas's **Rect Transform**, set **Width** `1000`, **Height** `600`.
2. Set its **Scale** to `0.001, 0.001, 0.001`. Now it's 1 m × 0.6 m.
3. Set **Position** to `0, 1.5, 2`: eye level, two metres away.

### Step 3: Add a button to it

1. Right-click the canvas → **UI → Button - TextMeshPro**. If asked to import **TMP
   Essentials**, click **Import**.
2. Make the button bigger (Width `300`, Height `100`) and change its text.
3. In the button's **On Click ()** event, click **+**, drag `Surprise` in, and choose
   **GameObject → SetActive (bool)** again, this time unticked, so this button hides it.

Press Play, point at the button, and press the trigger key.

**You should see:** the button highlights when you point at it, and hides the cube when
you click.

**Menu comfort tips:** keep text large (at 2 m, aim for at least 48 px text on a canvas
at 0.001 scale), keep panels in front of the player rather than off to the side, and
don't attach menus rigidly to the head, which is uncomfortable.

## Interaction layers (worth knowing)

Every interactor and interactable has an **Interaction Layer Mask**. They only
interact if their layers overlap. That's how the teleport ray avoids grabbing your cup,
and the grab ray avoids teleporting you. You'll use this in chapter 6.

## If something's wrong

| Problem | Fix |
|---|---|
| Can't grab anything | Check the object has an XR Grab Interactable and a collider, and that an XR Interaction Manager is in the scene |
| The grabbed object lags or jitters | Try Movement Type Instantaneous or Kinematic |
| Menu can't be clicked | The canvas needs a Tracked Device Graphic Raycaster (use GameObject → XR → UI Canvas), and the Event System needs an **XR UI Input Module** |
| Menu is huge or invisible | Canvas scale isn't 0.001, or it's behind you. Check its position |
| Text is pink or missing | Import TMP Essentials (Window → TextMeshPro → Import TMP Essential Resources) |

## Checkpoint

- [ ] I can pick up and throw the block, and I understand the three movement types
- [ ] The button shows a hidden object when selected
- [ ] The block snaps into the slot
- [ ] The floating menu responds to the controller ray
- [ ] I know what interaction layers are for

Next: [Moving around →](06-locomotion.md)
