# Chapter 7: Writing scripts for VR

[← Chapter 6](06-locomotion.md) · [Guide home](README.md) · Next: [Build and run on a Quest →](08-build-to-quest.md)

**Time:** 1 hour. **Needs:** the scene from chapter 6. No prior C# needed.

So far everything has been done with components and the Inspector. That covers a lot,
but for your own behaviour you write **scripts** in C#. This chapter teaches the parts
you need, one small script at a time, and ends with how to test your code without
putting a headset on.

---

## How a Unity script works

A script is a C# class that inherits from **MonoBehaviour**. You attach it to a
GameObject like any other component. Unity then calls certain methods on it for you, at
certain times:

| Method | When Unity calls it | Use it to |
|---|---|---|
| `Awake()` | Once, as soon as the object loads | Find other components, set up your own state |
| `OnEnable()` | Each time the object or component is switched on | Subscribe to events |
| `Start()` | Once, just before the first frame | Anything that needs other objects' `Awake` to have run |
| `Update()` | **Every frame**, 72+ times a second | Things that change continuously |
| `OnDisable()` | When switched off | Unsubscribe from events |

To create a script: in the Project panel, make a folder `Assets/Scripts`, right-click
it → **Create → Scripting → MonoBehaviour Script** (or **Create → C# Script** in older
versions). **The file name must match the class name exactly.** Double-click to open it
in your code editor.

## Script 1: make something spin

Create `Spinner.cs`:

```csharp
using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = 45f;

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
```

Save, go back to Unity, and drag the script onto the block from chapter 5. Press Play.

**What's going on:**

- `[SerializeField]` shows the field in the Inspector, so you can change the speed
  without editing code.
- `transform` is this object's position, rotation and scale.
- `Time.deltaTime` is how many seconds the last frame took. Multiplying by it makes the
  speed the same whether the app runs at 72 or 120 frames per second. **Always use it
  for anything that moves over time.**

## Script 2: a method a button can call

In chapter 5 the button could only show or hide. Here's a proper on/off toggle.

```csharp
using UnityEngine;

public class VisibilityToggle : MonoBehaviour
{
    [SerializeField] GameObject target;

    public void Flip()
    {
        target.SetActive(!target.activeSelf);
    }
}
```

1. Add it to any object, and drag `Surprise` into its **Target** slot.
2. On your button's **Select Entered** event (or the menu button's **On Click**), pick
   this object and choose **VisibilityToggle → Flip**.

**Why `public`:** events in the Inspector can only call public methods.

## Script 3: react to being grabbed, and buzz the controller

This one changes colour while held and makes the controller vibrate when you pick it
up. Vibration is called **haptics**.

```csharp
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class GrabFeedback : MonoBehaviour
{
    [SerializeField] Color heldColor = Color.yellow;
    [SerializeField, Range(0f, 1f)] float hapticStrength = 0.5f;
    [SerializeField] float hapticSeconds = 0.1f;

    XRGrabInteractable _grab;
    Renderer _renderer;
    Color _normalColor;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _renderer = GetComponent<Renderer>();
        _normalColor = _renderer.material.color;
    }

    void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        _renderer.material.color = heldColor;

        // Vibrate whichever controller did the grabbing.
        if (args.interactorObject is XRBaseInputInteractor controller)
            controller.SendHapticImpulse(hapticStrength, hapticSeconds);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        _renderer.material.color = _normalColor;
    }
}
```

Add it to the block.

**What's new:**

- `[RequireComponent]` makes Unity add an XR Grab Interactable if one's missing.
- `GetComponent<T>()` finds another component on the same object. It's slow-ish, so do
  it **once in `Awake`** and keep the result, never in `Update`.
- `selectEntered.AddListener(...)` subscribes to the grab event in code, the same event
  you used in the Inspector in chapter 5. Subscribe in `OnEnable`, unsubscribe in
  `OnDisable`, always as a pair.

**On XR Interaction Toolkit 2.x**, the class names differ: remove the `.Interactables`
and `.Interactors` `using` lines, and use `XRBaseControllerInteractor` instead of
`XRBaseInputInteractor`.

## Script 4: read a controller button directly

Sometimes you want a button press that isn't about touching an object, like opening a
menu. Unity's **Input System** handles this with **actions**: named inputs like "Jump"
that you bind to physical buttons.

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class ToggleOnButton : MonoBehaviour
{
    [SerializeField] InputActionReference button;
    [SerializeField] GameObject target;

    void OnEnable()
    {
        button.action.performed += OnPressed;
        button.action.Enable();
    }

    void OnDisable()
    {
        button.action.performed -= OnPressed;
    }

    void OnPressed(InputAction.CallbackContext context)
    {
        target.SetActive(!target.activeSelf);
    }
}
```

In the Inspector, click the circle next to **Button** and pick an action. To try it
out, pick **XRI Right Interaction/Activate** (the right trigger) from the Starter
Assets. For a real app, create your own action for a dedicated button, like the menu
button, so it doesn't clash with grabbing: **Create → Input Actions**, add a **Button**
action, and use the binding dropdown to pick the controller button you want.

## Keep your game rules out of MonoBehaviours

Here's the most useful habit for bigger projects. Put your **rules** in a plain C#
class that doesn't know about Unity objects, and keep the MonoBehaviour thin. Then you
can test the rules in a second, with no scene and no headset.

Example: a keypad that opens a door when you press the right code.

```csharp
// Plain C#. No MonoBehaviour, no transform, nothing Unity-specific.
public class CombinationLock
{
    readonly int[] _code;
    int _position;

    public bool IsOpen { get; private set; }

    public CombinationLock(params int[] code)
    {
        _code = code;
    }

    public void Press(int digit)
    {
        if (IsOpen) return;

        if (digit == _code[_position])
        {
            _position++;
            if (_position == _code.Length) IsOpen = true;
        }
        else
        {
            // Start again, but count this press if it's the first digit.
            _position = digit == _code[0] ? 1 : 0;
        }
    }
}
```

The MonoBehaviour just connects buttons to it and opens the door:

```csharp
using UnityEngine;

public class Keypad : MonoBehaviour
{
    [SerializeField] GameObject door;
    readonly CombinationLock _lock = new CombinationLock(1, 2, 3);

    // Each keypad button's Select Entered event calls this with its digit.
    public void Press(int digit)
    {
        _lock.Press(digit);
        if (_lock.IsOpen) door.SetActive(false);
    }
}
```

## Testing your rules

Unity has a **Test Runner** that runs small checks called tests.

1. Move `CombinationLock.cs` into its own folder, `Assets/Scripts/Logic`.
2. Right-click that folder → **Create → Scripting → Assembly Definition**. Name it
   `MyVR.Logic`. An **assembly definition** groups a folder of scripts so other code,
   like tests, can refer to it.
3. **Window → General → Test Runner** → **EditMode** tab → **Create EditMode Test
   Assembly Folder**. This makes a `Tests` folder with its own assembly definition.
4. Select the test assembly definition file in that folder. Under **Assembly Definition
   References**, click **+**, add `MyVR.Logic`, then click **Apply**.
5. In the `Tests` folder, create `CombinationLockTests.cs`:

```csharp
using NUnit.Framework;

public class CombinationLockTests
{
    [Test]
    public void OpensWithTheRightCode()
    {
        var keypad = new CombinationLock(1, 2, 3);
        keypad.Press(1);
        keypad.Press(2);
        keypad.Press(3);
        Assert.IsTrue(keypad.IsOpen);
    }

    [Test]
    public void AWrongDigitStartsAgain()
    {
        var keypad = new CombinationLock(1, 2, 3);
        keypad.Press(1);
        keypad.Press(9);
        keypad.Press(2);
        keypad.Press(3);
        Assert.IsFalse(keypad.IsOpen);
    }
}
```

Click **Run All** in the Test Runner.

**You should see:** two green ticks.

<details>
<summary><b>Check yourself:</b> why move the rules into a plain class? The Keypad MonoBehaviour works fine.</summary>

To test the Keypad you'd need a scene, a door object, buttons and a way to press them.
To test CombinationLock you need one line. When a bug turns up, like a wrong digit not
resetting properly, you can write a failing test for it, fix it, and know it stays
fixed. That's much faster than putting the headset on every time.

</details>

## Keep data in ScriptableObjects

Values you'll want to tweak, like speeds, colours, codes and text, can live in a
**ScriptableObject**: an asset file that holds data, editable in the Inspector.

```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "My VR/Room Settings")]
public class RoomSettings : ScriptableObject
{
    public float spinSpeed = 45f;
    public Color highlight = Color.yellow;
}
```

Right-click in the Project panel → **Create → My VR → Room Settings** to make one.
Scripts can then have a `[SerializeField] RoomSettings settings;` field. Anyone on your
team can change the values without touching code, and several objects can share one
settings file.

## Script habits for smooth VR

On a headset, a sudden pause while C# cleans up memory (called **garbage collection**)
shows up as a visible hitch. So in `Update`, and anything else that runs every frame:

- **Don't create new objects, lists or strings.** Create them once in `Awake`.
- **Don't call `GetComponent` or `Find`.** Look things up once and keep the result.
- **Don't build strings** like `"Score: " + score` every frame. Only update text when
  the value changes.
- **Prefer events to checking every frame.** Script 3 reacts to a grab event instead of
  asking "am I grabbed yet?" 72 times a second.

## If something's wrong

| Problem | Fix |
|---|---|
| "Can't add script component" | The file name and class name don't match exactly |
| Red errors after saving a script | Read the first error in the Console. Unity won't run anything until it's fixed |
| `XRBaseInputInteractor` not found | You're on XR Interaction Toolkit 2.x. See the note under script 3 |
| Method doesn't appear in the event dropdown | It isn't `public`, or it takes a parameter type the Inspector can't provide |
| Tests can't see your class | The test assembly definition doesn't reference your logic assembly definition |

## Checkpoint

- [ ] I know when Awake, OnEnable, Start and Update run
- [ ] I've written a script, attached it, and changed a field in the Inspector
- [ ] A button calls my own public method
- [ ] Grabbing the block changes its colour and vibrates the controller
- [ ] My rules live in a plain class, and I've run a test on them
- [ ] I know the four habits that keep scripts from causing hitches

Next: [Build and run on a Quest →](08-build-to-quest.md)
