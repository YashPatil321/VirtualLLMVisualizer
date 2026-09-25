# Chapter 3: Set up XR

[← Chapter 2](02-install-and-create-project.md) · [Guide home](README.md) · Next: [Your first VR scene →](04-first-vr-scene.md)

**Time:** 30 minutes. **Needs:** your project from chapter 2, open, with a clean
Console.

This chapter installs the three layers from chapter 1 (XR Plugin Management, OpenXR,
and the XR Interaction Toolkit) and configures them for Quest. It's the most
click-heavy chapter. Go slowly and check each "You should see".

---

## Step 1: Switch the build platform to Android first

**File → Build Profiles** (older versions: **File → Build Settings**). Select
**Android**, then click **Switch Platform**.

**Why first:** switching platform makes Unity reprocess every texture in the project.
In an empty project that takes seconds. In a big one it can take an hour. Do it now,
while the project is empty.

**You should see:** Android marked as the active platform.

## Step 2: Learn the Package Manager's two views

**Window → Package Manager.** The left side has two groups that trip everyone up:

- **In Project** (or "Project → All Packages"): what's **already installed**.
- **Unity Registry** (under "Sources"): **everything you can install**.

If you search for a package and get "No results", check which view you're in. You're
almost certainly searching only installed packages.

For each package, the right side shows **Install** if it's not installed, or buttons
like **Manage**, **Locate** or **Remove** if it is.

## Step 3: Turn on XR Plugin Management

1. **Edit → Project Settings**, then click **XR Plug-in Management** near the bottom
   of the list.
2. If you see an **Install XR Plugin Management** button, click it and wait.

**You should see:** a page with tabs for each platform: a monitor icon (Windows, Mac,
Linux) and an Android robot.

## Step 4: Enable OpenXR for Android

1. Click the **Android** tab.
2. Under **Plug-in Providers**, tick **OpenXR**. Unity installs the OpenXR package
   automatically. Wait for it.
3. Once it appears, also tick **Meta Quest feature group** (it shows up under OpenXR).

**You should see:** OpenXR ticked, with the Meta Quest feature group ticked below it.
You may see a yellow or red warning icon. Step 7 fixes those.

## Step 5: Enable OpenXR for Windows too

Click the **monitor** tab and tick **OpenXR** there as well.

**Why:** this lets you test in Unity's Play mode with a real headset connected to your
PC through Meta Quest Link, which is much faster than building to the headset every
time. It's optional, but useful once you have a headset.

## Step 6: Configure OpenXR

In Project Settings, click **XR Plug-in Management → OpenXR**, then the **Android** tab.

1. **Enabled Interaction Profiles:** click **+** and add **Oculus Touch Controller
   Profile**. If you'll use Quest Pro or Quest 3 controllers specifically, also add the
   **Meta Quest Touch Pro** or **Touch Plus** profiles.
2. **OpenXR Feature Groups:** make sure **Meta Quest Support** is enabled.
3. **Render Mode:** choose **Single Pass Instanced** (it may say "Multi-view").

**Why interaction profiles:** they tell OpenXR which controllers exist and what buttons
they have. With no profile, the app runs but your hands don't respond.

**Why Single Pass Instanced:** remember the scene is drawn twice, once per eye. This
setting draws both eyes in one pass, which roughly halves the drawing cost. On a
standalone headset that's a big deal.

Repeat the interaction profile step on the **monitor** tab if you enabled Windows.

## Step 7: Run Project Validation

Click **XR Plug-in Management → Project Validation**. It lists settings that are wrong
for XR, with a **Fix** button next to each and a **Fix All** button at the top.

Click **Fix All**. Then read what's left. Some items can't be fixed automatically and
tell you what to do.

**You should see:** no red errors left. A few yellow warnings are normal.

## Step 8: Install the XR Interaction Toolkit

1. Package Manager → **Unity Registry** → search **XR Interaction Toolkit** →
   **Install**.
2. If it asks to enable the new **Input System** or restart the editor, say yes.
3. If it asks to update the **Interaction Layer** settings (for teleporting), say yes.

## Step 9: Import the samples

Still on the XR Interaction Toolkit page in Package Manager, open the **Samples** tab.
Click **Import** next to:

- **Starter Assets**: a ready-made player rig with hands, rays, teleporting and input
  settings. You'll use it in every scene.
- **XR Device Simulator**: lets you play your VR scene with a keyboard and mouse, no
  headset needed.

**You should see:** a new folder in your Project panel, `Assets/Samples/XR Interaction
Toolkit/<version>/`, containing `Starter Assets` and `XR Device Simulator`.

<details>
<summary><b>Check yourself:</b> why import a sample instead of building the player rig yourself?</summary>

The player rig has dozens of parts: tracked camera, two controllers, rays, direct grab
colliders, teleport rays, turn and move providers, and input bindings for every button.
The Starter Assets version is built and tested by Unity. Start from it, and change it
once you understand it.

</details>

## If something's wrong

| Problem | Fix |
|---|---|
| No XR Plug-in Management in Project Settings | Install it from Package Manager → Unity Registry → "XR Plugin Management" |
| No Meta Quest feature group checkbox | Tick OpenXR first, and wait for it to finish installing |
| Red items in Project Validation won't go away | Read each one. Most say exactly which setting to change |
| Controllers don't respond later | Missing interaction profile (step 6) |
| Editor asks to restart | Save your scene, let it restart |

## Checkpoint

- [ ] Android is the active build platform
- [ ] OpenXR is ticked on the Android tab, with the Meta Quest feature group
- [ ] An interaction profile is added, and Render Mode is Single Pass Instanced
- [ ] Project Validation shows no red errors
- [ ] XR Interaction Toolkit is installed, with Starter Assets and XR Device Simulator imported

Next: [Your first VR scene →](04-first-vr-scene.md)
