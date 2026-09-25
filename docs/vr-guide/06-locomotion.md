# Chapter 6: Moving around

[← Chapter 5](05-interaction.md) · [Guide home](README.md) · Next: [Writing scripts for VR →](07-scripting.md)

**Time:** 30 minutes. **Needs:** the scene from chapter 5.

Players can walk around their real room, but your virtual world is probably bigger than
their living room. **Locomotion** is how they move further. It's also the thing most
likely to make people feel sick, so this chapter covers comfort as much as mechanics.

---

## Why moving in VR can make people sick

Your eyes see motion. Your inner ear, which senses balance, feels none. When those two
disagree, many people feel queasy. The more sudden or unexpected the motion, the worse
it is. Accelerating, and turning smoothly, are the worst offenders.

That's why VR has several ways to move, with different comfort levels:

| Method | How it works | Comfort |
|---|---|---|
| **Teleport** | Point, release, you're there instantly | Most comfortable. Default for beginners |
| **Snap turn** | Turn in fixed steps (e.g. 45°) instantly | Very comfortable |
| **Continuous move** | Joystick walks you smoothly, like a game | Can cause sickness. Offer it as an option |
| **Smooth turn** | Joystick rotates you smoothly | The most likely to cause sickness |

**Good rule:** teleport and snap turn by default, and let players switch on continuous
move and smooth turn if they want them.

## What the Starter Assets rig already has

The XR Origin from the Starter Assets comes with locomotion already set up: a
**Locomotion Mediator** that makes sure only one kind of movement happens at a time,
plus providers for teleporting, moving and turning, all wired to the joysticks. You
mostly need to tell it **where** people are allowed to go.

## Step 1: Let people teleport onto the floor

1. Select `Floor`.
2. Add the component **Teleportation Area**.

**You should see**, in Play mode: push the joystick forward (or use the simulator's
teleport keys), and a curved ray appears. Where it hits the floor, a marker shows. Let
go, and you're there.

**Why the floor needs a collider:** the teleport ray finds its landing spot by hitting
colliders. The plane already has one. Imported models may not.

## Step 2: Add a fixed teleport spot

Sometimes you want people to stand in an exact place and face an exact direction, like
in front of an exhibit.

1. Create a **Cylinder**, scale `0.5, 0.01, 0.5`, and put it somewhere on the floor.
   Name it `ViewingSpot`.
2. Add **Teleportation Anchor**.
3. Rotate it so its blue arrow (forward) points at whatever the player should face.

**You should see:** teleporting onto the disc snaps you to its centre, facing that way.

## Step 3: Stop people teleporting onto the table

Right now the teleport ray can land anywhere with a collider, including the tabletop.
Fix it with interaction layers from chapter 5:

1. The **Teleportation Area** on the floor has an **Interaction Layer Mask**. Make sure
   it's set to **Teleport**.
2. The table has no teleport component, so it's already excluded. If you ever add a
   Teleportation Area to something by mistake, remove it.

If the ray still shows a valid marker on surfaces you don't want, check the teleport
interactor's **Raycast Mask** on the XR Origin's teleport ray, and exclude those
objects' **physics layer**.

## Step 4: Choose turning style

Find the turn providers on the XR Origin (search the Hierarchy for **Turn**):

- **Snap Turn Provider**: set **Turn Amount** (45° is a common, comfortable choice).
- **Continuous Turn Provider**: set **Turn Speed**.

Enable one and disable the other to choose the default.

## Step 5: Add a comfort vignette for smooth movement

A **tunneling vignette** darkens the edges of the view while the player moves. It
narrows what their eyes see moving, which noticeably reduces sickness.

1. From the Starter Assets, drag the **Tunneling Vignette** prefab onto the **Main
   Camera** inside the XR Origin, as a child.
2. Select it. On the **Tunneling Vignette Controller**, add the move and turn providers
   to its **Locomotion Vignette Providers** list.

**You should see**, with continuous move on: the edges of the view darken while you
move and clear when you stop.

<details>
<summary><b>Check yourself:</b> your app has a moving platform the player rides on, like a lift. What's the comfort risk, and one way to reduce it?</summary>

The player sees motion they didn't cause and can't control, which is the worst case for
sickness. Keep it slow and steady, avoid sudden starts and stops, give them something
fixed to look at (like the lift's walls moving with them), and consider fading to black
and teleporting instead.

</details>

## Comfort rules worth following

- **Never move the camera without the player choosing to.** No camera shakes, no
  cutscenes that take control of the view.
- **Keep the horizon level.** Tilting the world is very uncomfortable.
- **Accelerate instantly or not at all.** Constant speed is easier than speeding up.
- **Fade to black** for big jumps between places, like scene changes.
- **Let people choose.** Put comfort settings in a menu.

## If something's wrong

| Problem | Fix |
|---|---|
| No teleport ray appears | Check the XR Origin came from Starter Assets, and that the Locomotion objects on it are enabled |
| Ray appears but can't land | The surface needs a collider and a Teleportation Area or Anchor |
| Teleporting lands you inside the floor | The XR Origin's Tracking Origin Mode isn't Floor (chapter 4) |
| Both snap and smooth turn happen | Disable one of the two turn providers |

## Checkpoint

- [ ] I can teleport around the floor, and onto a fixed viewing spot
- [ ] I can't teleport onto the table
- [ ] I've picked snap or smooth turning on purpose
- [ ] Continuous movement shows a comfort vignette
- [ ] I can explain why teleport is the most comfortable default

Next: [Writing scripts for VR →](07-scripting.md)
