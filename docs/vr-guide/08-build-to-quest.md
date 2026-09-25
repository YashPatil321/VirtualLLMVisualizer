# Chapter 8: Build and run on a Quest

[← Chapter 7](07-scripting.md) · [Guide home](README.md) · Next: [Performance and comfort →](09-performance-and-comfort.md)

**Time:** 45 minutes the first time. **Needs:** a Quest 2, 3 or 3S, a USB-C data cable,
and a phone with the Meta Horizon app.

Everything so far ran inside Unity. Now you'll turn your project into an Android app
and install it on the headset. The first time takes a while. After that, it's one click.

---

## Step 1: Turn on Developer Mode

A Quest only installs apps from the Meta store unless you switch on **Developer Mode**.

1. Create a free developer account at Meta's developer site, and create or join an
   **organisation** there. Developer Mode requires one.
2. On your phone, open the **Meta Horizon** app (formerly the Meta Quest app), make sure
   your headset is paired, and find **Developer Mode** in the headset's settings.
3. Switch it on, then restart the headset.

Meta renames these menus from time to time. If you can't find the switch, search
Meta's developer docs for "enable developer mode".

## Step 2: Connect the headset

1. Plug the headset into your PC with the **data** cable.
2. Put the headset on. You'll see **Allow USB debugging?** Tick **Always allow from
   this computer** and choose **Allow**.

**Check it from the PC:** open PowerShell and run:

```powershell
& "$env:ProgramFiles\Unity\Hub\Editor\<version>\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" devices
```

Replace `<version>` with your Unity version folder. `adb` is Android's device tool, and
Unity installed it with the Android module.

**You should see** a line ending in `device`. If it says `unauthorized`, you haven't
accepted the prompt inside the headset yet.

## Step 3: Check the player settings

**Edit → Project Settings → Player**, then the **Android** tab (robot icon), then
**Other Settings**. Project Validation from chapter 3 fixes most of these, but check
them:

| Setting | Value | Why |
|---|---|---|
| **Color Space** | Linear | What URP and correct lighting expect |
| **Graphics APIs** | Vulkan (or OpenGLES3) at the top | Vulkan is generally faster on Quest |
| **Scripting Backend** | IL2CPP | Needed for 64-bit Android |
| **Target Architectures** | ARM64 only | The Quest is 64-bit ARM. Untick everything else |
| **Minimum API Level** | What Meta currently requires | Android 10 (API 29) is a common floor; Meta raises this over time, so check before publishing |
| **Texture Compression** (in Build Profiles) | ASTC | The format the Quest's GPU reads natively |

Also fill in **Company Name** and **Product Name** at the top of Player settings. They
become the app's name and ID.

## Step 4: Build and run

1. **File → Build Profiles**, select **Android** (it should already be active from
   chapter 3).
2. Check your scene is in the **Scene List**.
3. Under **Run Device**, pick your Quest. Click **Refresh** if it isn't listed.
4. Click **Build And Run**. Choose a folder (make a `Builds` folder next to `Assets`,
   which git ignores) and a file name.

The first build takes several minutes. Later builds are faster.

**You should see:** the app launch on the headset by itself, with your room around you.
Walk around. Grab the block. Teleport. Press the button.

## Step 5: Finding the app again later

On the headset, open your **Library**. Apps you installed yourself are under a filter
or section called **Unknown Sources**.

## Test in Play mode with the headset (optional, faster)

Building takes minutes. On a Windows PC with a capable graphics card you can skip
building while you iterate:

1. Install the **Meta Horizon Link** app on the PC and connect the headset with Link
   (cable or Air Link).
2. Make sure OpenXR is ticked on the **Windows** tab of XR Plug-in Management (chapter
   3, step 5), and set Meta as the OpenXR runtime in the Link app's settings if asked.
3. Press **Play** in Unity. The scene appears in the headset.

**Big warning:** this runs on your PC, not the headset. It'll run much faster than the
real thing. **Always check performance with a real build** (chapter 9).

<details>
<summary><b>Check yourself:</b> your app runs perfectly over Link, but stutters as a real build. Why?</summary>

Over Link, your PC's graphics card does the drawing. As a build, the headset's own
phone-class chip does it. Performance only means something on the device.

</details>

## If something's wrong

| Problem | Fix |
|---|---|
| Android isn't in Build Profiles | The Android module isn't installed (chapter 2, step 3) |
| Quest not listed under Run Device | Cable carries only power, Developer Mode is off, or USB debugging wasn't allowed. Check `adb devices` |
| `adb devices` says `unauthorized` | Put the headset on and accept the prompt |
| Build fails mentioning architecture | Scripting Backend is still Mono, or ARMv7 is ticked. Use IL2CPP and ARM64 only |
| App opens to a black screen | OpenXR isn't ticked on the **Android** tab, or the scene has no XR Origin |
| App opens as a flat window floating in VR | Same: XR isn't enabled for Android |
| Hands don't move | No interaction profile added (chapter 3, step 6) |
| App isn't in the library | Look under Unknown Sources |

## Checkpoint

- [ ] Developer Mode is on, and `adb devices` shows the headset as `device`
- [ ] Player settings match the table
- [ ] My app runs on the headset, and I can grab, press and teleport in it
- [ ] I know where to find the app again, and why Link performance doesn't count

Next: [Performance and comfort →](09-performance-and-comfort.md)
