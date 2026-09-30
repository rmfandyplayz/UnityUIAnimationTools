# UI Animation Player

Make UI animations in the Inspector, without writing code. For Unity UGUI, built on DOTween Pro.

You give a UI object named animations (`Show`, `Hide`, `Press`…), each built from simple steps like "fade in over 0.25s", and play them by name: from a button's On Click, from another animation finishing, or from code.

## 📖 [Read the full guide on the wiki](https://github.com/rmfandyplayz/UnityUIAnimationTools/wiki)

Step-by-step tutorials, examples, every feature explained, and troubleshooting.

---

## Installation

Needs **Unity 2021.3+**, **DOTween Pro**, uGUI and TextMeshPro.

1. Import DOTween Pro.
2. Run **`Tools → Demigiant → DOTween Utility Panel → Setup DOTween…`**, with the UI and TextMeshPro modules ticked. **Don't skip this**: without it, this folder won't compile, and the errors point at this code rather than the real cause.
3. Copy this whole folder anywhere under `Assets/`, **including the `.meta` files**. Without them, every object already using the tool loses its component.
4. **Add Component → UI Animation Player** on any UI object.

Don't add an `.asmdef` to this folder: it silently removes the DOTween shortcuts the tool is built on.

More detail: [Installation](https://github.com/rmfandyplayz/UnityUIAnimationTools/wiki/Installation).

---

## Code API

```csharp
using rmf_claude.DOTweenUI;

[SerializeField] private UIAnimationPlayer anim;

anim.Play("Show");
anim.Play("Hide", () => gameObject.SetActive(false));   // only on a natural finish
yield return anim.Play("Show").WaitForCompletion();     // Play returns the DOTween Sequence
```

`UIAnimationPlayer`:

```csharp
// Playing
Sequence Play(string name);
Sequence Play(string name, Action onComplete);                    // only when it finishes naturally
Sequence Play(string name, Action<UIAnimationEndReason> onEnd);   // always, exactly once, with the reason
void PlayAnimation(string name);                                  // void version, for UnityEvents

// Stopping
void Stop(string name, bool complete = false);   // complete: jump to the end, as if finished
void StopAll(bool complete = false);
void StopAnimation(string name);                 // void version, for UnityEvents

// Checking
bool IsPlaying(string name);
bool IsAnyPlaying { get; }
bool Has(string name);

// Setting up
void ApplyFromState(string name);   // jump to an animation's starting values without playing
void CaptureBaseline();             // re-read resting values (not while animating)

// Changing values at runtime (step = index from the top, starting at 0)
bool SetTo(string name, int step, float / Vector3 / Color / string value);
bool SetFrom(string name, int step, float / Vector3 / Color / string value);
void ClearOverrides(string name);   // back to the Inspector's values
```

`UIAnimationEndReason`: `Completed` · `Interrupted` · `Stopped` · `Disabled` · `Destroyed` · `NotFound`.

```csharp
UIAnimationAudio.SetShared(AudioSource source);   // route sound steps through your own (mixer) source
```

Explanations and examples: [Code API](https://github.com/rmfandyplayz/UnityUIAnimationTools/wiki/Code-API).
