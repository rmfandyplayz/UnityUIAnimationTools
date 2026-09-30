# UI Animation Player

Make UI animations in the Inspector, without writing code. For Unity UGUI, built on DOTween Pro.

You give a UI object named animations (`Show`, `Hide`, `Press`…), each built from simple steps like "fade in over 0.25s", and play them by name: from a button's On Click, from another animation finishing, or from code.

## 📖 [Read the full guide on the wiki](https://github.com/rmfandyplayz/UnityUIAnimationTools/wiki)

Step-by-step tutorials, examples, every feature explained, and troubleshooting.

---

## Installation

Needs **Unity 2022.3+**, **DOTween Pro**, uGUI and TextMeshPro. Installs through the Package Manager, which is also how you update it.

1. Import DOTween Pro.
2. Open **`Tools → Demigiant → DOTween Utility Panel`** and click **Setup DOTween…**, with the UI and TextMeshPro modules ticked.
3. In the same panel, click **Create ASMDEF**.
4. **`Window → Package Manager → + → Install package from git URL…`** and paste:
   ```
   https://github.com/rmfandyplayz/UnityUIAnimationTools.git?path=/DOTweenAnimationPlayer#release
   ```
5. **Add Component → UI Animation Player** on any UI object.

**Moving over from a copied folder?** Delete the old copy from `Assets/` *before* step 4. Two copies clash. Objects keep their components, because the package uses the same script IDs.

**Errors like `'RectTransform' does not contain a definition for 'DOAnchorPos'`** (seven of them, all in `UIAnimationStep.cs`) mean step 2 or 3 was skipped. Click the missing button and they go away.

### Updating

**`Window → Package Manager`**, select **DOTween UI Animation Player** under *In Project*, and click **Update** at the top right. Unity checks GitHub and installs the newest release. Nothing changes until you click it. (The refresh button under the list doesn't check packages installed from GitHub.)

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
