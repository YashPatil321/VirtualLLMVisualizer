# Lesson 4: Unity project hygiene that saves you later

[← Lesson 3](03-testing-without-unity.md) · [Index](README.md) · Next: [From nothing to an open project on Windows →](05-windows-setup.md)

**You'll learn:** what `.meta` files are and why losing one silently breaks things,
how assembly definitions keep tests and editor tools out of your build, how to set up
git for a Unity project before the first binary lands, and how to generate data assets
from a script.

**Needs:** nothing installed. About 30 minutes.

---

## `.meta` files are not clutter

Every file and folder under `Assets/` gets a `.meta` file next to it. Open one:

```yaml
fileFormatVersion: 2
guid: 19087cf1b75144e18b7c2499c1dedeaa
MonoImporter:
  ...
```

That `guid` is the asset's identity. Unity never refers to assets by path. It refers to
them by GUID. Here's the top of `Assets/Data/AssemblySequence.asset`:

```yaml
  m_Script: {fileID: 11500000, guid: 19087cf1b75144e18b7c2499c1dedeaa, type: 3}
```

That line says "this asset is an instance of the script whose GUID is 19087cf1...".
The GUID matches `AssemblySequence.cs.meta`. Lose that `.meta` file, and Unity generates
a new one with a new GUID. The asset now points at nothing. There's no error, just an
empty inspector and a warning about a missing script.

That's why `CLAUDE.md` says never commit an asset without its `.meta`.

<details>
<summary><b>Predict first:</b> you create a new Unity project and copy this repo's <code>Assets/</code> folder into it using File Explorer. What goes wrong?</summary>

Probably nothing, if File Explorer copies every file. But `.meta` files are easy to
lose: some copy tools skip them, some people filter them out because they look like
noise, and some git setups ignore them by mistake. If any are dropped, every asset that
referenced them breaks silently.

That's why this project's setup goes the other way (lesson 5): the repo stays put, and
the two folders Unity generates get copied **into** it. The bootstrap script then
checks every asset still has its `.meta` and refuses to finish if one is missing.

</details>

When the AI agent wrote `.meta` files by hand, it also checked that **no two GUIDs were
the same**. A duplicate makes Unity silently remap one of them, which breaks references
in a way that is very hard to track down.

## Only `Assets/` exists, as far as Unity cares

The original design put the sample trace at `data/sample-trace.json`, at the top of the
repo. The trace player takes a `TextAsset` field, and Unity only imports files under
`Assets/`. A file outside it can't be dragged into the inspector or included in a
build.

So the V5 acceptance line, "sample-trace.json loads and validates", was impossible as
written. The file moved to `Assets/Data/`, and every document that mentioned the old
path was updated in the same commit.

## Assembly definitions

An **assembly definition** (`.asmdef`) groups the scripts in a folder into their own
compiled assembly. This project has three:

| asmdef | Folder | Why it's separate |
|---|---|---|
| `OCS.VR` | `Assets/Scripts/` | The runtime code that ships |
| `OCS.VR.Editor` | `Assets/Editor/` | Editor tools, like the scene builder. Editor platform only, so it never ends up in the APK |
| `OCS.VR.Tests.EditMode` | `Assets/Tests/EditMode/` | Tests. Needs NUnit and the Test Framework, and has the `UNITY_INCLUDE_TESTS` constraint |

The original test file noted that it "needs an asmdef" and didn't have one, so the
tests could never have run. Adding the asmdef was the fix.

## git for Unity, set up before the first model

Two files, both written before any art existed:

**`.gitignore`** keeps out what Unity regenerates: `Library/`, `Temp/`, `Obj/`,
`Build/`, `Logs/`, `UserSettings/`. `Library/` alone can be gigabytes. It also has one
line that matters more than it looks:

```
!/**/*.meta
```

That forces `.meta` files to be tracked even if some other rule would catch them.

**`.gitattributes`** does two things. It sends binary art through Git LFS, so a
repository doesn't grow by the full size of a model every time it changes:

```
*.fbx    filter=lfs diff=lfs merge=lfs -text
*.png    filter=lfs diff=lfs merge=lfs -text
```

And it tells git to merge Unity's YAML files with Unity's own merge tool:

```
*.unity  text merge=unityyamlmerge eol=lf
*.prefab text merge=unityyamlmerge eol=lf
```

Moving existing history into LFS later is painful, so set it up before the first
binary lands.

## Things not to hand edit

`CLAUDE.md` says: don't hand edit `Packages/manifest.json`. Install packages through
the Package Manager instead. The manifest is only part of the picture. The Package
Manager also resolves dependencies, updates a lock file and imports package content.
Editing the manifest by hand can leave those out of step, especially behind an editor
that's already running.

For the same reason, the AI agent didn't try to fake `ProjectSettings/` or `Packages/`
from scratch. It let Unity generate them (lesson 5).

## Pin the editor version

`ProjectSettings/ProjectVersion.txt` records the exact editor version:

```
m_EditorVersion: 6000.6.1f1
```

If two people open the project in different versions, each one rewrites this file on
every open, and it fights in every commit. So the version is pinned in
`docs/setup-runbook.md`, along with an open question: the constraints call for an LTS
release, and 6.6 might not be one. That's cheap to fix while nothing is built, and
expensive once scenes and baked lighting exist.

## Generating data assets from a script

The four `.asset` files in `Assets/Data/` were generated by a Python script, not
created in the editor. A ScriptableObject asset is plain YAML: a fixed header, the
script's GUID, then the fields:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  ...
  m_Script: {fileID: 11500000, guid: 19087cf1b75144e18b7c2499c1dedeaa, type: 3}
  m_Name: AssemblySequence
  steps:
  - stepId: frame
    partName: "Open air frame"
    kind: 0
    ...
```

This is powerful, and risky in exactly one way: a field name typo is silent. That's why
lesson 3's harness binds every key back to the real C# type. Generate, then verify.

> **Where it went wrong**
>
> When the project was first opened in Unity, Unity created two assets in the root of
> `Assets/`: a default volume profile and the URP global settings. The first commit
> from the Windows machine ran `git add ProjectSettings Packages`, which is what the
> lesson said to do, so those two assets weren't committed. Neither was the scene built
> in lesson 7.
>
> Always run `git status` after Unity opens a project for the first time, and commit
> what it created under `Assets/` along with the `.meta` files.

> **Try it**
>
> Pick any `.asset` file in `Assets/Data/`. Find its `m_Script` GUID, then find which
> `.cs.meta` file has that GUID:
>
> ```bash
> grep -rl "guid: <paste the guid>" Assets --include=*.cs.meta
> ```

## Checkpoint

- [ ] I can explain what a GUID in a `.meta` file is for, and what happens if it's lost
- [ ] I know why the trace had to move into `Assets/`
- [ ] I can name the three asmdefs and why each is separate
- [ ] My own Unity repo has a `.gitignore` and LFS rules in `.gitattributes` before any art
- [ ] I run `git status` after Unity generates files, and commit them with their `.meta`

Next: [From nothing to an open project on Windows →](05-windows-setup.md)
