# Build VR apps with Unity: a step by step guide

This guide takes you from nothing installed to a VR app running on a Meta Quest
headset. You'll build a small VR room where you can look around, pick things up and
throw them, press buttons, use a floating menu and teleport. Along the way you'll learn
what each piece does and why, so you can build your own apps afterwards.

It's written for beginners. If you've never opened Unity, start at chapter 1 and go in
order. Every new term is explained the first time it appears.

## What you need

- A **Windows PC**. macOS works for most chapters, but building to a headset is
  easiest from Windows. About 25 GB of free disk space.
- **A Meta Quest 2, 3 or 3S** for chapter 8. You can do chapters 1 to 7 without one,
  using a simulator that runs inside Unity.
- A **USB-C cable that carries data**, not just charging, for chapter 8.
- Some patience for downloads. Unity and its Android tools are large.

You don't need to know C# already. Chapter 7 teaches the parts you need.

## The chapters

| # | Chapter | What you'll have at the end |
|---|---|---|
| 1 | [How VR apps work](01-how-vr-apps-work.md) | A clear picture of the pieces, and the words for them |
| 2 | [Install Unity and create a project](02-install-and-create-project.md) | Unity installed with Android support, and an empty project |
| 3 | [Set up XR](03-set-up-xr.md) | The VR packages installed and configured for Quest |
| 4 | [Your first VR scene](04-first-vr-scene.md) | A room you can look around, tested in the simulator |
| 5 | [Grabbing, buttons and menus](05-interaction.md) | Objects you can pick up and throw, a button, a floating menu |
| 6 | [Moving around](06-locomotion.md) | Teleporting, walking and turning, with comfort settings |
| 7 | [Writing scripts for VR](07-scripting.md) | Your own C# behaviours, controller input, vibration, tests |
| 8 | [Build and run on a Quest](08-build-to-quest.md) | Your app running on the headset |
| 9 | [Performance and comfort](09-performance-and-comfort.md) | An app that runs smoothly and doesn't make people feel sick |
| 10 | [Using an AI coding agent with Unity](10-ai-agents-and-unity.md) | Claude Code connected to your Unity editor |

## How each chapter works

- **Steps** are numbered. Do them in order.
- **You should see** tells you what success looks like, so you know a step worked
  before moving on.
- **Why** explains the reason behind a step. It's what makes your next project easier.
- **Check yourself** questions have the answer hidden. Think first, then click.
- **If something's wrong** lists the problems people most often hit at that point.
- **Checkpoint** at the end lists what you should have done. Tick them off in your own
  copy.

## A note on versions

This guide is written for **Unity 6** (version numbers starting 6000), the **XR
Interaction Toolkit 3.x** and **OpenXR**. Unity moves menus around between versions.
If a step names a menu or setting you can't find, type its name into the search box at
the top of the Project Settings window or the Package Manager. That almost always finds
it. On the older XR Interaction Toolkit 2.x the ideas are the same, but some class names
in code differ; chapter 7 notes where.

## Glossary

| Term | Meaning |
|---|---|
| **XR** | "Extended reality": the umbrella word for VR (virtual reality) and AR/MR (augmented, mixed). Unity uses it everywhere |
| **Headset / HMD** | Head mounted display. The thing you wear |
| **6DoF** | Six degrees of freedom. The headset tracks moving in 3 directions and rotating in 3, so you can lean, crouch and walk |
| **Standalone** | The headset runs the app itself, no PC needed. Quest apps are Android apps |
| **OpenXR** | An open standard that lets one app talk to many brands of headset |
| **XR Origin** | The object in your scene that represents the player: a camera for your head, plus your hands |
| **Interactor** | Something that can touch or point at things, like your hand or a laser pointer |
| **Interactable** | Something that can be touched, grabbed or pressed |
| **Locomotion** | Moving the player around the world: teleporting, walking, turning |
| **Frame rate** | How many images per second the app draws. VR needs a high, steady rate: 72, 90 or 120 |
| **URP** | Universal Render Pipeline. Unity's renderer built to run fast on phones and standalone headsets |
| **Prefab** | A saved, reusable GameObject. Drag it into any scene |
| **Package** | An add-on for Unity, installed through the Package Manager |
