# Chapter 2: Install Unity and create a project

[← Chapter 1](01-how-vr-apps-work.md) · [Guide home](README.md) · Next: [Set up XR →](03-set-up-xr.md)

**Time:** about an hour, mostly waiting for downloads. **Needs:** a Windows PC with
about 25 GB free.

---

## Step 1: Install Unity Hub

Unity Hub is the launcher that installs Unity versions and opens your projects.
Download it from [unity.com/download](https://unity.com/download), install it, and sign
in or create a free Unity account. A free Personal licence is fine for learning and for
small projects.

## Step 2: Skip the welcome tutorial

The Hub opens on a **Get set up** screen with a **Start project** button for an
in-editor tutorial. **Skip it for now.** It creates a standard project that isn't set
up for VR. You'll make the right kind of project in step 5.

## Step 3: Install the editor with Android support

1. Click **Installs** in the left sidebar, then **Install Editor**.
2. Pick a **Unity 6 LTS** version from the **Official releases** tab. LTS means "long
   term support": it gets bug fixes for years, which is what you want for a real
   project.
3. When it asks for modules, tick **Android Build Support**. Then expand it and also
   tick **Android SDK & NDK Tools** and **OpenJDK**.
4. Install. This takes a while.

**Why Android?** A Quest is an Android device. Without this module, you can't build for
it, and Unity won't tell you why. It just won't show Android as an option.

**You should see:** your editor listed under Installs with an **Android** badge next to
the others (like Windows and Web).

<details>
<summary><b>Check yourself:</b> you already installed Unity, and its badges only say "Web" and "Windows". What now?</summary>

You don't need to reinstall. Click **Manage** next to that version, then **Add
modules**, and tick Android Build Support with its two sub-items. The two sub-items are
the part people most often miss.

</details>

## Step 4: Decide your version, and write it down

Everyone working on a project should use the **exact same** Unity version. Unity
records it in the project, and if two people open it with different versions, it keeps
getting changed back and forth. Pick one, and write it in your project's README.

## Step 5: Create the project

1. Click **Projects**, then **New project**.
2. Choose the **Universal 3D** template. If it has a download icon, click that first.
   Avoid "3D" or "3D (Built-In Render Pipeline)". Those use the older renderer, which
   is too slow for a standalone headset.
3. Type a project name.
4. If the name box offers **Create new project** (cloud icon) and **Create new local
   project** (monitor icon), pick **local**. Cloud links the project to Unity's online
   services, which you don't need to start with.
5. **Location:** somewhere easy to find, like `C:\Users\<you>\UnityProjects`.
6. **Version control:** choose **Select none** if you'll use git yourself (step 7).
7. Click **Create project**.

**You should see:** the Unity editor open after a few minutes. The first launch is slow
because it's compiling shaders. Let it finish.

## Step 6: A two minute tour of the editor

| Panel | What it's for |
|---|---|
| **Hierarchy** (left) | Everything in the current scene, as a tree |
| **Scene** (centre) | Where you arrange things. Right-click and drag to look around, then use WASD to fly |
| **Game** (tab next to Scene) | What the camera sees when you press Play |
| **Inspector** (right) | Settings for whatever you've selected |
| **Project** (bottom) | All the files in your project |
| **Console** (tab next to Project) | Messages, warnings and errors. Check it often |

The **Play** button at the top runs your scene. Press it again to stop. **Anything you
change while playing is lost when you stop.** Everybody gets caught by this once.

## Step 7: Set up git (recommended)

Git keeps a history of your project so you can undo mistakes and share it on GitHub.

1. Install [Git for Windows](https://git-scm.com/download/win).
2. Open **Windows PowerShell** from the Start menu. Use a normal window, **not "Run as
   administrator"**. Using an administrator window here causes an ownership error later.
3. In your project folder, create a file called `.gitignore` containing at least:

   ```
   /[Ll]ibrary/
   /[Tt]emp/
   /[Oo]bj/
   /[Bb]uild/
   /[Bb]uilds/
   /[Ll]ogs/
   /[Uu]ser[Ss]ettings/
   *.apk
   ```

   `Library/` is Unity's cache. It can be several gigabytes, and Unity rebuilds it
   automatically, so it should never go into git. GitHub also has a ready-made Unity
   `.gitignore` you can copy.

4. Then run:

   ```powershell
   cd C:\Users\<you>\UnityProjects\<YourProject>
   git init
   git add .
   git commit -m "Empty URP project"
   ```

**Why .meta files matter:** you'll notice every file in `Assets/` has a matching
`.meta` file. That file holds the asset's ID, and Unity uses the ID, not the file name,
to connect things together. **Always commit `.meta` files with their assets.** If one
goes missing, whatever referenced that asset silently breaks.

## If something's wrong

| Problem | Fix |
|---|---|
| No Android option anywhere | Add the Android module and both sub-items (step 3) |
| The Hub's Projects list is empty but you made a project | Use **Add → Add project from disk** and pick the project folder |
| git says "detected dubious ownership" | The folder was created from an administrator window. Run the `git config --global --add safe.directory ...` line that git prints, then use normal windows from now on |
| Changes vanished after pressing Play | You edited during Play mode. Stop first, then edit |
| Package search finds nothing | You're searching installed packages. See chapter 3, step 1 |

## Checkpoint

- [ ] Unity 6 is installed with the Android badge
- [ ] I have a Universal 3D project open, with a clean Console
- [ ] I know what each editor panel is for
- [ ] (recommended) My project is in git with a `.gitignore`, and `.meta` files are committed

Next: [Set up XR →](03-set-up-xr.md)
