# Chapter 10: Using an AI coding agent with Unity

[← Chapter 9](09-performance-and-comfort.md) · [Guide home](README.md)

**Time:** 45 minutes. **Needs:** a Unity project open on a Windows PC, and a Claude
Pro, Max, Team or Enterprise account.

An **AI coding agent** is an assistant that can read your project, write and change
code, run commands and, once connected, control the Unity editor for you. This chapter
sets up **Claude Code** with Unity and covers how to get good results from it, including
the ways it can quietly go wrong.

---

## What an agent is good at, and what it isn't

| Good at | Not good at, or can't do |
|---|---|
| Writing and explaining scripts | Seeing what your app looks like, unless connected to the editor |
| Finding bugs by writing and running tests | Knowing how it feels in a headset |
| Repetitive editor work: creating objects, wiring references | Judging comfort, scale or fun |
| Setting up project structure, git, assembly definitions | Knowing about menus that changed after it was trained |
| Writing docs and READMEs | Putting on the headset for you |

The last row of the right-hand column matters most. The agent can build and check a lot,
but you are the only one who can say "this makes me feel sick" or "this is too small to
read".

## Step 1: Install Claude Code

Open a normal PowerShell window (not "Run as administrator") and run:

```powershell
irm https://claude.ai/install.ps1 | iex
```

Then **close PowerShell and open a new window**, and check:

```powershell
claude --version
```

If it says `claude` isn't recognised, the installer may have told you its folder isn't
on your **PATH** (the list of places Windows looks for commands). Add it with:

```powershell
[Environment]::SetEnvironmentVariable("PATH", [Environment]::GetEnvironmentVariable("PATH","User") + ";$env:USERPROFILE\.local\bin", "User")
```

and open a new window again. A changed PATH only applies to windows opened afterwards.

## Step 2: Start it in your project folder

```powershell
cd C:\Users\<you>\UnityProjects\<YourProject>
claude
```

The first time, a browser opens so you can log in.

**Why the project folder:** Claude Code reads a file called `CLAUDE.md` from the folder
it starts in. That's where you tell it the rules of your project.

## Step 3: Write a CLAUDE.md

Create `CLAUDE.md` in your project folder. The agent reads it at the start of every
session. Good rules are specific and say **why**. Here's a starting point for a Quest
project:

```markdown
# CLAUDE.md

## What this is
A VR app for Meta Quest built in Unity 6 with URP and the XR Interaction Toolkit.

## Hard constraints
- Target: standalone Quest 2 and up. Android, ARM64, IL2CPP.
- 72 fps minimum on the headset. A dropped frame is a bug, not polish.
- URP only. No real-time shadows or post-processing without measuring on device first.

## Conventions
- Game rules go in plain C# classes with edit mode tests. MonoBehaviours stay thin.
- No allocation, GetComponent or Find in Update.
- Tunable values go in ScriptableObjects, not hard-coded numbers.

## Don't
- Don't hand edit Packages/manifest.json. Use the Package Manager.
- Don't commit Library/, Temp/, Obj/ or Build/.
- Don't commit an asset without its .meta file.
- Don't edit scene files directly while the editor is open. Use the editor connection.
```

**Why rules need a reason:** "keep it fast" gets ignored. "A dropped frame is a bug"
gets acted on. The same goes for people.

## Step 4: Connect Claude Code to the Unity editor

Unity can run an **MCP server**. MCP (Model Context Protocol) is a standard way for an
AI agent to use another program's tools. Unity's server offers tools like "list the
scene", "create an object", "add a component" and "read the Console". With it, the
agent works through the editor properly instead of editing files behind its back.

**This only works when Claude Code runs on the same computer as Unity.** The editor
listens only to programs on the same machine. A cloud-based agent can't reach it.

There are two ways to connect. Try the first.

**Route A: Unity's official Claude Code plugin.** At the time of writing, Unity
publishes a Claude Code plugin that bundles editor control, the Unity CLI and
Unity-specific skills. At the Claude Code prompt, type:

```
/plugin
```

search for Unity, and install the official plugin.

**Route B: Unity's AI Assistant package.** Unity's MCP server ships in the **AI
Assistant** package. Install it from Package Manager → Unity Registry. Then **Edit →
Project Settings → AI → Unity MCP Server** appears. Make sure the bridge is running,
and use its integrations section to configure Claude Code.

Note that ticking "Use AI Assistant" when creating a project doesn't always install the
package. If there's no **AI** section in Project Settings, it isn't installed.

## Step 5: Check it's really connected

1. Make sure Unity is open and **not in Play mode**. Play mode blocks scene changes.
2. Restart Claude Code: type `/exit`, then run `claude` again.
3. Type `/mcp`. You should see a Unity server listed as connected.
4. Ask it: **list the scene hierarchy**.

**You should see:** the objects in your open scene, like `XR Interaction Setup`,
`Floor` and `Table`.

<details>
<summary><b>Check yourself:</b> could you expose Unity's MCP server to the internet so a cloud agent can reach it?</summary>

Technically, with a tunnel. Don't. That server has full control of your editor: it can
create, change and delete anything in your project. On a public address, anyone who
finds it has the same control. Run the agent on your own machine instead.

</details>

## Step 6: Ask for things in a way that works

A few prompt patterns that make a big difference:

**Start by getting it oriented:**

> Read CLAUDE.md. Then list the scene hierarchy and tell me what you think each object
> is for, before changing anything.

**Ask for a check before a fix:**

> Run all EditMode tests in the Test Runner and tell me exactly how many pass and fail,
> with the full output for failures. Don't fix anything yet.

**Ask for a fix you can trust:**

> The block falls through the table when the scene starts. Find out why and show me the
> cause before changing anything. Then fix it, and tell me how you checked.

**Ask for editor work:**

> Add a grabbable 10 cm cube on the table with an XR Grab Interactable, Kinematic
> movement, and the Wood material. Then list the hierarchy so I can see it.

**Keep it honest:**

> For each step you give me, say whether you've checked it or are going from memory.

## Where agents go wrong, and how to catch it

These all happened while building a real Quest project with an agent:

| What went wrong | How it was caught |
|---|---|
| Gave menu paths from memory that didn't exist in the installed Unity version | A screenshot of the actual window |
| Assumed a checkbox had installed a package; it hadn't | Looking at Project Settings and the package list |
| Placed objects by guessing coordinates without being able to see the scene, and the result looked like a random pile of boxes | Opening the scene and looking at it |
| Wrote a script for Windows it couldn't run itself | It said so, and asked for the output |
| Told the user to commit some generated files but not others | Running `git status` and comparing |

The patterns to remember:

- **An agent that can't see guesses about anything visual.** Menus, layout, sizes. Send
  screenshots. Connect it to the editor so it can look.
- **Almost every mistake is caught by running or looking at something**, not by reading
  the agent's explanation again. Ask for tests. Press Play. Check `git status`.
- **Setup steps for fast-moving tools go stale.** Treat install instructions, from an
  agent or from any guide including this one, as "probably right, check each step".
- **Commit before a long editor session.** The agent has full editor access, and you'll
  want something to compare against or roll back to.

## Checkpoint

- [ ] `claude --version` works in a new window
- [ ] My project has a CLAUDE.md with constraints, conventions and don'ts
- [ ] `/mcp` shows Unity connected, and the agent can list my scene
- [ ] I've asked for a check before a fix at least once
- [ ] I know the three habits that catch an agent's mistakes: screenshots, running things, and `git status`

[Back to the guide home](README.md)
