# Setup Runbook: Unity + Quest 2 + MCP Editor Control

Purpose: get from nothing to an empty scene running on the Quest 2, with Claude able to drive the editor. This is milestone V1 in the VR plan. Do this before any design work, because a broken toolchain is much easier to find in an empty project than in a half built one.

---

## 0. Prerequisites

- Quest 2 with Developer Mode enabled through the Meta Quest mobile app (requires a developer account, free)
- USB C cable that carries data, not just power
- `adb` available, which comes with the Android platform tools the Unity Android module installs
- Enough disk. The editor plus the Android module is large

---

## 1. Unity CLI and license

```bash
unity --version
unity auth status --format json      # if signed out: unity auth login
unity license status --format json   # if none active: unity license activate
```

A personal license is fine for this.

---

## 2. Install the editor with the Android module

Quest 2 is an Android target, so the `android` module is required. Without it the build target will not appear and the failure message is unhelpful.

```bash
unity install lts --module android --yes --accept-eula
```

This takes several minutes. Start it and go do something else. Confirm afterward:

```bash
unity editors --installed --format json
```

Use Unity 6 LTS. The editor control package in step 5 requires Unity 6.0 or newer, so an older LTS costs you the MCP workflow.

---

## 3. Create the project

List the real template ids rather than guessing them, then pick the 3D URP one:

```bash
unity templates list
unity projects create "OCSVisualizer" --path ~/projects --editor-version <version> --template <id>
```

URP matters here. The built in render pipeline will run on Quest but you lose the mobile optimizations that make hitting 72 Hz realistic.

---

## 4. Source control

Use Git with LFS, because 3D models and textures are binary and will wreck a plain Git repo over time.

```bash
cd ~/projects/OCSVisualizer
git init
git lfs install
```

Make sure the Unity `.gitignore` is in place before the first commit and confirm that `Library/`, `Temp/`, `obj/`, and `Build/` are not staged. Every script and asset must be committed together with its `.meta` file, or the project breaks for whoever clones it next.

---

## 5. Editor control over MCP

This is what lets Claude Code create GameObjects, wire component references, place the
eight cards, and read the Unity console, instead of me doing it by hand in the Inspector.

Important: MCP controls the **editor**. It does not replace writing C#. Scripts still land
as files in `Assets/Scripts/`. What MCP removes is the scene assembly work, which is the
tedious half.

### Option A: Unity's official MCP server (start here)

It ships with Unity's in editor AI assistant package, so there is nothing to clone.

1. In Unity, go to `Edit > Project Settings > AI > Unity MCP`
2. Check that Unity Bridge shows **Running** with a green indicator. It starts
   automatically when the editor loads. If it shows Stopped, select Start.
3. Expand **Integrations**, pick Claude Code, and select **Configure**. That writes the
   client config for you.
4. Restart Claude Code so it picks up the connection.

If Claude Code is not in the auto configure list, add a server entry pointing at the Unity
relay binary, which Unity installs to `~/.unity/relay/` on startup. Pass `--mcp` as a
command line argument to the relay executable.

### Option B: MCP for Unity (Coplay), if the official one is not available

Package Manager, `+`, **Add package from git URL**:

```
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
```

Then `Window > MCP for Unity > Toggle MCP Window` and click **Start Server**. A terminal
opens running the HTTP server on `http://127.0.0.1:8080`. Leave it open. Register it with
Claude Code once:

```bash
claude mcp add-json unityMCP '{"type":"http","url":"http://localhost:8080/mcp"}' --scope user
```

Use `--scope local` from the project root to register it for this project only.

### Verify

Ask Claude Code to list the scene hierarchy. If it comes back with objects, the bridge is
live.

### Things that will bite

- **Play mode blocks scene operations.** Stop the game before running scene building
  commands or they fail with unhelpful errors.
- **Multiple projects open means it may connect to the wrong one.** Keep one editor open.
- **Objects must exist before they can be referenced.** If a command targets a GameObject
  by name and it is not there, it fails. Ask for the hierarchy first.
- **MCP bypasses file level guardrails.** It has full editor access and goes through the
  Unity API rather than the filesystem, so it can create, modify, and delete any asset.
  Commit before a long session so there is something to diff against.
- **A compile error stops everything.** Fix C# errors before expecting editor commands to
  work.

---

## 6. XR packages

Install these through the Package Manager, not by hand editing `manifest.json`:

- **OpenXR Plugin** with the Meta Quest feature group enabled
- **XR Interaction Toolkit**, which gives grab, snap sockets, and ray interaction without writing any of it
- **XR Plugin Management**, then enable OpenXR under the Android tab

The XR Interaction Toolkit samples are worth importing. The starter assets include a working XR rig and input actions, which saves a day of input plumbing.

---

## 7. Android build settings for Quest 2

| Setting | Value | Why |
|---|---|---|
| Platform | Android | Quest runs Android |
| Scripting backend | IL2CPP | Required for ARM64 |
| Target architecture | ARM64 only | Quest 2 will reject anything else |
| Minimum API level | Meta's current minimum | Check Meta's docs, this moves |
| Graphics API | Vulkan | Better on Quest 2 than OpenGL ES |
| Stereo rendering mode | Single Pass Instanced | Roughly halves draw call cost |
| Texture compression | ASTC | Mobile GPU format |
| Color space | Linear | URP expects it |

---

## 8. First build to device

```bash
adb devices     # headset should be listed, accept the USB debugging prompt in the headset
```

Then build and run from the editor to the connected device. Success looks like: an APK installs, launches from the Unknown Sources section of the Quest library, and you can look around a grey scene with a floor and your controllers visible.

That is V1. Commit it before touching anything else.

---

## 9. Verification checklist

- [ ] `unity editors --installed` lists a Unity 6 LTS editor with the android module
- [ ] Project opens with no console errors
- [ ] `unity status` reports a connected editor in state ready
- [ ] `adb devices` lists the Quest 2
- [ ] Empty URP scene runs on the headset at a stable frame rate
- [ ] Controllers tracked and visible
- [ ] Repo committed with `.meta` files, without `Library/`
- [ ] Screenshot or clip of the headset view saved as evidence for the sprint

---

## 10. Things that will probably go wrong

| Symptom | Likely cause |
|---|---|
| Android is not an option in Build Settings | Android module not installed with the editor |
| Build fails on architecture | Scripting backend still Mono, or ARMv7 still checked |
| Black screen in the headset | OpenXR not enabled under the Android tab in XR Plugin Management |
| `adb devices` shows unauthorized | USB debugging prompt not accepted inside the headset |
| `unity status` times out | Safe Mode from a C# compile error |
| Terrible frame rate in an empty scene | Built in render pipeline instead of URP, or multi pass stereo |
