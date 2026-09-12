# UI Animation Utility

Data-driven DOTween animations for UGUI, authored in the Inspector instead of in code.

You define **named** animations (`Show`, `Hide`, `Hover`, `Attention`, `TransitionOut`…) on a component and play them by name. The framework attaches no meaning to any name — it just builds and plays the steps you authored.

---

## Dropping this into a new project

Copy the whole `DOTweenAnimationPlayer` folder anywhere under `Assets/`. **Keep the `.meta` files** — they carry the script GUIDs, which is what lets a prefab you authored with this tool in one project still find its components when you drag it into the next one.

Needs **DOTween** (Pro for edit-mode preview), **uGUI** and **TextMeshPro**. Unity 2021.3+.

Everything lives in the namespace **`rmf_claude.DOTweenUI`**, so any script that plays an animation needs:

```csharp
using rmf_claude.DOTweenUI;
```

Nothing in the folder is left in the global namespace, so it cannot collide with a `UIAnimation` the host project already has.

**After importing DOTween, run its setup panel** — `Tools → Demigiant → DOTween Utility Panel → Setup DOTween…`. This is not optional and not the same as importing DOTween: `DOAnchorPos`, `DOFade`, `DOColor` and `DOSizeDelta` don't live in `DOTween.dll`, they're generated into `DOTween/Modules/DOTweenModuleUI.cs` by that panel. Skip it and this folder won't compile, with errors pointing at *this* code rather than at the real cause.

**Don't add an `.asmdef`** to this folder unless you know what you're doing. Those generated modules land in `Assembly-CSharp-firstpass`, and an assembly definition cannot reference a predefined assembly — so adding one silently removes every DOTween UI shortcut this framework is built on.

---

## Components

| Component | Add it to | Why |
|---|---|---|
| **UI Animation Player** | any UI GameObject | Holds the named animations. This is the one you need. |
| **UI Material Instance** | an Image / RawImage / TMP text | Only needed for shader property animation. Gives the element its own material so tweens can never write to the shared project asset. |

And one asset, which is entirely optional:

| Asset | Create with | Why |
|---|---|---|
| **UI Animation Set** | `Create → UI Animation → Animation Set` | One authored library of animations that many players share. See [Sharing animations](#sharing-animations-between-objects). |

---

## Creating an animation

1. **Add Component → UI Animation Player**.
2. `+` on **Animations**, set **Name** to `Show`.
3. `+` on that animation's **Steps**, pick a **Type**. The inspector collapses to only the fields that type uses.

Each step's collapsed header reads like a timeline line: `AFTER   Logo (Scale)   0.25s   Out Back` — start mode, the object it drives, the property in parentheses, duration, ease.

### Step types

`AnchoredPosition` · `LocalPosition` · `Scale` · `Rotation` · `CanvasGroupAlpha` · `GraphicColor` · `GraphicAlpha` · `MaterialFloat` · `MaterialColor` · `PunchScale` · `PunchAnchoredPosition` · `ShakeAnchoredPosition` · `SetActive` · `PlaySound` · `SizeDelta` · `OffsetMin` · `OffsetMax`

`SetActive` and `PlaySound` are **instant** — they happen at a point in the timeline rather than over one, so they show a `Delay` but no `Duration` and no easing.

### Sizing a RectTransform

Four step types write the same two rect corners from different directions. That's what makes them useful, and also what makes them fight:

| Type | Drives | Reach for it when |
|---|---|---|
| `AnchoredPosition` | Position; size untouched | Moving something |
| `SizeDelta` | Size, around the pivot | A panel that expands, a bar that grows |
| `OffsetMin` | The **left and bottom** edges | Driving one pair of edges |
| `OffsetMax` | The **right and top** edges | Driving one pair of edges |

`OffsetMax` is measured **inward-negative** — the Inspector's Right and Top fields are `-offsetMax.x` and `-offsetMax.y`. So a stretched rect inset 12px on all sides is `Offset Min (12, 12)` with `Offset Max (-12, -12)`.

**Don't animate two of these on the same target at once.** Unity stores one rect and derives all four views from it, so they overwrite each other instead of combining. Measured on a 100×40 rect: setting `offsetMin` from `(-50,-20)` to `(-10,-10)` *also* moved `sizeDelta` to `(60,30)` and `anchoredPosition` to `(20,5)`, with nothing else touched. Pick the one view that says what you mean and drive only that.

`SizeDelta` is size **relative to the anchors**, so on a stretched rect it behaves as padding rather than as pixels. Un-stretch the anchors first if you want a literal pixel size.

All three new types take X/Y (Z is unused) and offer **Snapping** for whole-pixel results.

---

## Targets

Each step shows exactly one target slot, chosen by its type.

**Leave it empty to target the GameObject the player is on.** Drag in a child or sibling to target something else.

Below the slot is an optional **Target Path**, and between them that's the whole system:

| Filled in | What the step drives |
|---|---|
| The target slot | Exactly that object. Wins over everything below. |
| **Target Path** only | The object at that path relative to the player, e.g. `Panel/Icon`. |
| Neither | The GameObject the player is on. This is the ordinary case. |

Target Path uses `transform.Find`, so names must match exactly, and — usefully — **inactive objects are found**, which is what lets a `SetActive` step switch a hidden child back on.

**`..` works, and it is what makes the common layout portable.** A very ordinary way to author this is to put the player on a child object called something like `Animations` and point its steps at the *button above it*. That target is not a descendant, so it looks unshareable — but `..` is the player's parent and `../../Sibling` is a sibling of it, chained as deep as you like:

| Target Path | Resolves to |
|---|---|
| *(empty)* | the player's own GameObject |
| `Panel/Icon` | a descendant |
| `..` | the player's parent — the `Animations`-child layout |
| `../../Other` | a sibling of the parent |

Verified against Unity 6000.3: `Find("..")` returns the parent and `Find("../../Name")` walks up twice and back down. Note that `..` is resolved one segment at a time like any other, so `../Self` means "a child of my parent named `Self`", not "me".

Paths are resolved **once**, at `Awake`, not per frame. A path that matches nothing logs **one** warning naming the player and the path, and the step is skipped. It is deliberately not treated as "fall back to the player" — a mistyped `Panel/Icon` that silently scaled the whole panel would be far harder to spot than a step that visibly does nothing.

An animation whose steps use only empty slots and paths is **portable**: it works on any object with the right children, which is what makes it worth sharing.

- `GraphicColor` / `GraphicAlpha` take a **Graphic**, which covers `Image`, `RawImage`, legacy `Text` **and TextMeshProUGUI**. There is no separate TMP step type.
- `AnchoredPosition`, `PunchAnchoredPosition`, `SizeDelta`, `OffsetMin` and `OffsetMax` show an X/Y field — Z is not used.
- `PlaySound` takes an **AudioSource**, and empty means something slightly different — see [Sound](#sound).
- If a target is missing at play time the step is skipped with a console warning naming the animation and step index. It won't throw.

---

## Sequential vs parallel

Every step has a **Start** field:

- `AfterPrevious` → `Sequence.Append` — runs after everything before it.
- `WithPrevious` → `Sequence.Join` — runs alongside the previous step.

Per-step **Delay** works with both. Read the step list top to bottom and that's your timeline.

---

## Easing

Each step uses one of two easing sources:

- **Ease** — a DOTween preset. `Out*` eases decelerate into the end value and suit most UI.
- **Use Custom Curve** — tick it and the Ease dropdown is replaced by an `AnimationCurve` field.

For the curve, time runs 0→1 left to right, and value `0` = the FROM value, `1` = the TO value. Going above 1 or below 0 overshoots, which is how you build a bounce or an anticipation dip.

Unity's curve editor has a **preset bar along the bottom** — click a swatch to apply a shape, or use the arrow at the right of the bar to save your own. That's a native Unity feature, so curve shapes are reusable without any extra asset.

Punch and shake steps ignore both — DOTween drives their oscillation internally, so there's no ease field on them.

Neither is changed by the [mirror commands](#mirroring-an-animation) — a mirrored `Hide` keeps the ease its `Show` was authored with.

---

## Frame rate

By default an animation moves smoothly, changing a little every rendered frame. Tick **Play At Custom FPS** on an animation and an **FPS** box appears below it — from then on that animation advances in discrete steps, for a stop-motion or flipbook look.

```
Loops                      1
Loop Type                  Restart
Play At Custom FPS         ✔
FPS                        12
```

`12` is the classic hand-drawn look, `24` is film, `6`–`8` is deliberately crunchy. Setting it above your display's refresh rate does nothing, because there's no frame in between to hold on.

**It's a look, not a throttle.** The tween still updates every frame — it just keeps returning the same value until the next frame boundary, which is what stepped playback actually is. Nothing gets cheaper, and `Duration` means exactly what it did before.

Things worth knowing:

- **Steps still land exactly.** The end of a step is always sampled at its true end, so `To: Baseline` lands precisely on the resting value even when the `Duration` isn't a whole number of frames. That last frame is just shorter than the rest.
- **Every step shares one frame grid**, measured from the start of the animation rather than from each step's own start — so two steps staggered by an odd delay still tick together. Without that, a joined group reads as jitter rather than as stop-motion.
- **Custom curves are stepped too**, exactly like Ease presets.
- **Punch and shake are never stepped.** They drive their own oscillation internally rather than through the easing this works on, and overriding it breaks them silently. Same reason they have no Ease field.
- **`SetActive` and `PlaySound` still fire at their exact authored time**, not snapped to the grid — snapping them would quietly collapse a sub-frame stagger onto one frame.
- It's per animation, so a `Show` can be stepped while a `Hover` on the same object stays smooth.

The setting is carried across by the [mirror commands](#mirroring-an-animation).

---

## FROM / TO

Each endpoint has a **mode**:

| Mode | Meaning |
|---|---|
| `Absolute` | Use the value exactly as typed. |
| `Baseline` | The element's resting value, captured at `Awake`, **plus** the typed value as an offset. |
| `Current` | Whatever the value is when the tween starts, plus the typed value (DOTween relative). Only available on **To**, and only when **Use From** is off. |

**Use `To: Baseline` for anything that should land on its authored resting state.** Ten `Show`s in a row all land on exactly the same value, and if you later change the resting scale/position in the scene the animation follows automatically. This is what stops repeated Show/Hide from drifting.

**Use From** (unticked by default) enables the FROM endpoint. **Apply From Values Immediately** (on by default, per animation) snaps every FROM value the moment the animation starts rather than when each step begins — this is what prevents an element flashing at full opacity through a delayed step before jumping to 0.

Punch and shake steps have no FROM/TO — they show `Punch` / `Strength` instead, and ignore Ease (they carry their own).

---

## Sound

A **PlaySound** step fires a clip at its point in the timeline. Put one first in a `Press` animation and you have a button click; put one at `Delay 0.15` in a `Show` and it lands with the scale bounce.

```
▼ AFTER  ui_click (Play Sound)
    Type               Play Sound
    Start              After Previous
    Audio Source       None            ← see below
    Clip               ui_click
    Volume             1
    Pitch              1
    Pitch Variation    0.08
    Delay              0
```

**Which AudioSource plays it**, in order:

1. The step's **Audio Source** field, if you assigned one.
2. An `AudioSource` on the GameObject the player is on.
3. A shared 2D source the framework creates on first use.

Step 3 is the point of the whole thing — you can add a click sound to forty buttons without adding forty AudioSources. It appears in the hierarchy as **UI Animation Audio** under *DontDestroyOnLoad*, is 2D (so the AudioListener's position is irrelevant), and has `ignoreListenerPause` on.

**Pitch Variation** is `± ` on top of Pitch, rolled fresh each play. `0.08` is enough to stop a repeated click sounding like a machine gun.

Things worth knowing:

- Clips play via `PlayOneShot`, so overlapping UI sounds don't cut each other off, and **stopping an animation does not stop a sound it already started**. A sound scheduled for later in the sequence simply never fires.
- **Pitch is a property of the AudioSource, not of the one-shot.** Setting it also shifts any sound still playing on that same source. Imperceptible for clicks; if it matters, give that step its own AudioSource.
- Audio ignores `Time.timeScale`, so pause-menu sounds work with no extra setup.
- **For mixer routing** (a UI volume slider), either assign your own mixer-connected AudioSource on the step, or replace the shared one once at startup:

```csharp
UIAnimationAudio.SetShared(myUISfxSource);
```

- A `PlaySound` step with no **Clip** logs one warning at startup and is skipped.

---

## Copying animations and steps

Right-click a header in the Inspector:

| Right-click on | You get |
|---|---|
| An **animation** header | `Copy Animation` · `Paste Animation (overwrite)` · `Paste Animation Above` · `Paste Animation Below` · `Mirror Animation` · `Duplicate as Mirrored` |
| A **step** header | `Copy Step` · `Paste Step (overwrite)` · `Paste Step Above` · `Paste Step Below` · `Mirror Step` |
| The **Animations** list | `Paste Animation (add to end)` · `Paste Animation Mirrored (add to end)` |
| The **Steps** list | `Paste Step (add to end)` · `Paste Step Mirrored (add to end)` |

**Above / Below insert a new element** and shuffle the rest down, rather than overwriting the one you right-clicked — that's how you land a step in the middle of a list without adding a blank one at the end and dragging it up. The pasted `Start` mode comes across as copied, so pasting a `With Previous` step into a group is how you widen it.

The mirror commands are covered in [Mirroring an animation](#mirroring-an-animation).

This works **across GameObjects** — copy a `Press` animation off one button, select another, right-click its Animations list, paste. Object references survive the round trip, so a step pointing at a specific `Graphic` or holding an `AudioClip` still points at it after pasting. (Unity's own generic Copy/Paste on a property drops those references, which is why this exists.)

A pasted animation whose name already exists on the target gets ` 2` appended, because `Play()` resolves names first-match-wins and a duplicate would be silently unreachable.

The clipboard holds one animation and one step at a time, and lasts until you restart the Editor.

For bulk reuse, **Copy Component / Paste Component Values** on the whole player and prefab variants both still work as normal.

---

## Mirroring an animation

**Right-click a `Show` → `Duplicate as Mirrored`** and you get a `Show Mirrored` sitting under it, which you rename to `Hide`. It is ordinary authored data — every value is visible in the Inspector and yours to tune.

This rewrites the steps once, at author time — there is no runtime reverse mode, and nothing about playback changes. What you get is a second animation, and it is yours to diverge from as soon as it exists.

| Command | Does |
|---|---|
| `Mirror Animation` | Mirrors that animation in place |
| `Duplicate as Mirrored` | Appends a mirrored copy named `<name> Mirrored` |
| `Paste Animation Mirrored (add to end)` | Mirrors the clipboard on the way in — copy a `Show` off one object, paste a `Hide` onto another |
| `Mirror Step` | Flips one step, for fixing a single wrong direction |

### What it changes

| Forwards | Mirrored |
|---|---|
| Step order | Fully reversed — the last step becomes the first. Joined groups stay joined and stay whole, and their members reverse too. Reversing inside a group only changes how the list reads: joined steps all start from the same point, so their order in the list never affected timing |
| Staggered delays inside a joined group | Flipped, so the item that arrived last is the first to leave |
| `From` / `To` | Swapped, values and modes both |
| Ease preset | **Unchanged.** A mirrored `Hide` keeps the `Out Quart` its `Show` was authored with |
| Custom curve | **Unchanged** |
| `To: Current` relative offset | The same offset negated |
| `SetActive` on | `SetActive` off |
| Punch / shake | Unchanged — they already return to where they started |
| `PlaySound` | Keeps its clip. If a hide needs a different sound, swap it afterwards |

Everything outside the steps — `Loops`, `Loop Type`, `Play At Custom FPS`, `Interrupt Others`, `Notes`, `On Complete` — is carried across untouched.

**Easing is deliberately not mirrored.** A strict time-reversal would turn `Out Quart` into `In Quart`, but the house style here is that things decelerate into place in *both* directions, so the ease you authored is the ease you keep. Endpoints and timing are what mirror; how the motion feels is a separate choice. Change it by hand on the mirrored copy if you want the other reading.

### The one case it can't get right

A step with **Use From** off has no authored start, so there is nothing exact to mirror onto — its forward starting value was whatever the property happened to hold at the time.

For those steps the mirror does the best it can: `Use From` is switched **on** and set to the forward `To` (which *is* known), and `To` becomes `Baseline + 0`, the resting value. Then it **logs a warning naming each affected step**, because that second half is a guess. Click the warning to ping the object.

That guess is right for an animation authored away from rest, and a no-op for one that already ends at rest — if a mirrored step does nothing, this is why, and the fix is to type the `To` you actually want.

A single step's delay is also left alone. Delays are flipped *within* a joined group; a lone step's delay is a gap between groups, which can't be expressed on the step itself.

---

## Sharing animations between objects

Right-click copy/paste moves an animation between objects, but it makes a **copy** — retune the original and the forty copies stay as they were. When that stops being reasonable, put the animation in a **UI Animation Set** instead.

`Create → UI Animation → Animation Set` makes one. It holds the same list of animations, authored in the same inspector. Assign it to a player's **Shared** slot and that player can play everything in it.

```
UI Animation Player
  Use Unscaled Time                ✔
  Kill On Disable                  ✔
  Shared                           Menu Panels (UI Animation Set)   ← the library
  Animations                       0                                ← plus anything local
```

**Local animations win.** A player plays its own list first and then everything from the set whose name it hasn't already used, so one panel can override just the `Show` out of a shared set while still getting the shared `Hide`, `Press` and `Attention`. That override is the point of the feature; the asset inspector warns you when a player shadows one of its names, because otherwise you can spend ten minutes tuning a curve that never plays.

This is opt-in and it is not the default. Authoring straight onto the player is fewer clicks and is right for anything only one object does.

### What an asset can't hold

**A ScriptableObject cannot reference a scene object.** So the direct target slots are disabled when you author inside a set, with the reason shown under them. Use **Target Path** for anything relative — a child, or `..` for the parent — or leave the slot empty for the player's own GameObject. Those are enough to write a genuinely reusable animation, and needing them is what keeps a shared animation honest about being shared.

If a step arrives with a target anyway — pasting one copied off a player carries live references — the asset clears it and says so, rather than letting it serialize to null at the next save with nothing said.

### Previewing a set

An animation set has nothing of its own to animate, so its inspector borrows a scene player: drop one into **Preview On** and the Play / Start / Stop buttons run the ordinary player preview, with the same capture, restore, Undo entry and one-at-a-time rule. The player has to actually have the set in its Shared slot — it plays what its own merge produced, not what any asset happens to contain — and the buttons say so and disable themselves when it doesn't.

The Preview On slot is not saved into the asset. It couldn't be: a scene reference is the one thing an asset cannot keep, which is what the whole feature is working around.

### Things worth knowing

- Each player takes its **own copy** of the set's animations at `Awake`. It has to: resolved targets and the live sequence are per-object state, so two players sharing one asset would otherwise animate each other's objects. The cost is that **editing the set at runtime does not reach players that have already started** — and that assigning `Shared` from code after `Awake` does nothing.
- Right-click copy/paste and the mirror commands work on a set exactly as they do on a player, so an animation can move between the two in either direction.
- Two animations with the same name inside one set: the first wins, the second is unreachable, and the asset warns.

---

## Previewing

The Inspector has **Play / Start / Stop** buttons per animation, and they work **without entering play mode**. The list includes animations from the Shared set as well as local ones.

| Button | Does |
|---|---|
| `Play` | Runs the animation on the real scene objects |
| `Start` | Snaps just the `From` values on, so you can check a starting pose |
| `Stop and Restore` | Ends the preview and puts every value back |

Edit-mode preview animates **real objects in your open scene**, so it takes some care:

- Values are **captured before it starts and restored when it stops**, so a preview leaves nothing behind. Stop it before you save.
- The whole preview is **one Undo step** — `Ctrl+Z` is the escape hatch if something looks wrong.
- Starting a new preview restores the previous one first. That matters: it's what stops `To: Baseline` endpoints drifting a little further every time you press `Play`.
- Selecting another object ends the preview and restores.
- **`Play Sound` steps are skipped**, and **`On Complete` events do not fire** — an `On Complete` is a UnityEvent wired to arbitrary game code, and a preview has no business running that outside play mode.

In play mode the buttons just call the ordinary runtime API, so sound and `On Complete` behave normally.

---

## Notes

Each animation has a free-text **`Notes`** box. Nothing reads it — it's for you: what this animates, what plays it, why that one weird delay is there. It travels with copy/paste and mirroring like any other field.

---

## Playing from code

```csharp
[SerializeField] private UIAnimationPlayer anim;

anim.Play("Show");
anim.Play("Hide", () => gameObject.SetActive(false));   // fires on a natural finish only

// Play returns the live Sequence, so coroutines work:
yield return anim.Play("Show").WaitForCompletion();
```

Full API:

```csharp
Sequence Play(string name);
Sequence Play(string name, Action onComplete);
Sequence Play(string name, Action<UIAnimationEndReason> onEnd);   // always fires, exactly once

void Stop(string name, bool complete = false);
void StopAll(bool complete = false);

void PlayAnimation(string name);    // void wrappers, so UnityEvents can call them
void StopAnimation(string name);

bool IsPlaying(string name);
bool IsAnyPlaying { get; }
bool Has(string name);

void ApplyFromState(string name);   // snap to an animation's FROM values without playing
void CaptureBaseline();             // re-capture resting values at runtime
```

### Knowing how an animation ended

`Play(name, Action)` fires **only on a natural finish**. That's the right rule for an authored `On Complete`, and the wrong one for code:

```csharp
anim.Play("Hide", () => Destroy(gameObject));   // leaks the object if anything interrupts the hide
```

The third overload closes that. Its callback fires **exactly once, whatever happens** — never zero times, never twice — and says how it ended:

```csharp
anim.Play("Hide", reason =>
{
    if (reason == UIAnimationEndReason.Completed) Destroy(gameObject);
    else                                          gameObject.SetActive(false);
});
```

| `UIAnimationEndReason` | When |
|---|---|
| `Completed` | Ran to its natural end — or `Stop(name, complete: true)`, or an animation with nothing to play. The only reason that also fires `On Complete`. |
| `Interrupted` | Another `Play` cut it short: the same animation restarting, or a different one with **Interrupt Others**. |
| `Stopped` | `Stop`, `StopAll`, or a kill issued from outside the player such as `DOTween.KillAll`. |
| `Disabled` | The GameObject or the player component was disabled while it was running. |
| `Destroyed` | The player was destroyed while an animation was still live. |
| `NotFound` | No animation of that name exists, so nothing played. A typo is a runtime failure like any other, and a caller waiting on a callback that never comes is what this overload exists to prevent. |

Two things to know about it:

- **Destroying an *enabled* object reports `Disabled`, not `Destroyed`.** Unity runs `OnDisable` before `OnDestroy` and gives no way to know a destroy is coming, so that's reported honestly rather than guessed at. `Destroyed` is what you get when **Kill On Disable** is off, or the object was already inactive. If you only care whether the animation finished, compare against `Completed` and ignore the rest.
- **A kill from outside the player counts too.** `DOTween.KillAll()` or `DOTween.Clear()` elsewhere in the project resolves every armed callback as `Stopped` rather than leaving callers waiting.

The plain `Action` overload and the authored `On Complete` UnityEvent are **unchanged** by any of this — still a natural finish only, because that's what "finished" means to someone who wired up an event in the Inspector. When both are present, `On Complete` runs first and `onEnd` last, so an authored event still gets to run before a caller's `Destroy`.

One source-level wrinkle: `Play(name, null)` is now ambiguous between two overloads and needs a cast. `Play(name)` is unaffected.

### From UnityEvents

Unity's event dropdown only lists methods that **return void and take at most one argument**. That rules out `Play` (it returns a `Sequence`) and `Stop` (an optional parameter is still a parameter, so it reads as two). `PlayAnimation` and `StopAnimation` are void wrappers that do appear — and a void `Play(string)` can't be an overload, because C# won't overload on return type alone.

What a UI Animation Player offers in the dropdown:

| Entry | Does |
|---|---|
| `PlayAnimation (string)` | Plays the animation you type in the box |
| `StopAnimation (string)` | Stops it where it stands |
| `StopAll (bool)` | Stops everything on this player. Tick the box to complete rather than cut |
| `ApplyFromState (string)` | Snaps to an animation's FROM values without playing |
| `CaptureBaseline ()` | Re-captures resting values |

So a Button's **On Click** plays an animation with no glue script, and an animation's own **On Complete** starts the next one — chaining without code.

Chaining is safe even though the next animation's **Interrupt Others** tries to kill the one whose callback is currently running: an animation releases its sequence *before* firing callbacks, so by then there is nothing left to interrupt. Two things follow:

- An animation whose `On Complete` plays **itself** restarts cleanly instead of recursing. That's a loop, though — `Loops -1` says it far better.
- **`StopAnimation` never fires `On Complete`**, so stopping a chain stops it dead. Only a natural finish carries it on. That's the existing rule for interrupted animations, and it's what makes a chain interruptible at all.

`ApplyFromState` is the clean way to start hidden without authoring a separate state:

```csharp
private void Awake() => anim.ApplyFromState("Show");
```

In play mode the inspector shows **Play / Start / Stop** buttons per animation so you can tune timing without a test script. `Start` snaps to the FROM values without playing.

---

## Conflict and lifecycle behaviour

**Read this bit.**

- `Play(name)` kills that animation's own running sequence, and — because **Interrupt Others** is on by default — every *other* animation on the same player too. So `Play("Show"); Play("Hide");` leaves only `Hide` running. Untick **Interrupt Others** for something that should layer on top, like a looping pulse.
- **An interrupted animation never fires its callback.** Neither the `Action` nor the UnityEvent. If the callback fired, the animation genuinely finished. The `Action<UIAnimationEndReason>` overload is the exception and always fires — see [Knowing how an animation ended](#knowing-how-an-animation-ended).
- **Disabling the GameObject kills running animations** (`Kill On Disable`, on by default) — loops stop and callbacks do *not* fire. The next `Play` re-snaps its FROM values, so nothing ends up visually corrupted. Untick it to let animations run through a disable.
- Sequences are linked to the GameObject with `KillOnDestroy` and also killed in `OnDestroy`, so destroying objects or changing scenes leaves no orphaned tweens.
- **Use Unscaled Time** is on by default, so UI still animates while `Time.timeScale == 0`. Leave it on for pause menus.
- Baselines are captured once at `Awake`, before anything animates. If you move an element deliberately at runtime and want `Baseline` endpoints to follow, call `CaptureBaseline()` — but not mid-animation.

---

## Material / shader properties

Add **UI Material Instance** to the element. It clones the material at `Awake` and assigns the clone, so the shared project asset is never written to. `MaterialFloat` / `MaterialColor` steps target *that component* — there's no inspector slot that could point a tween at a shared asset by accident.

Set **Shader Property** to the property name (`_Progress`). It's resolved with `Shader.PropertyToID`.

Two real constraints:

- **TextMeshPro** ignores `Graphic.material` entirely, so the component uses TMP's own `fontMaterial` instead (and doesn't destroy it — TMP owns it). This works, but note you're animating TMP's material instance.
- **A stencil `Mask` breaks this.** UGUI takes a one-time cached copy of your material for stencil rendering and never re-syncs it, so animated properties won't reach the screen. Use a **RectMask2D** instead, or untick **Maskable** on the Graphic. The component logs a warning if it detects this.

Fullscreen transition overlays aren't inside masks, so the common case is unaffected.

---

## Example — Fade + Scale "Show"

Panel with a `CanvasGroup`, laid out in the scene at its final resting state. Every field below is shown exactly as the Inspector lists it, in order.

```
UI Animation Player
  Use Unscaled Time                ✔
  Kill On Disable                  ✔
  Animations                       1
    ▼ Show
        Name                       Show
        ▼ Steps                    2
            ▼ AFTER  Panel (self) (Canvas Group Alpha)  0.25s  Out Quad
                Type               Canvas Group Alpha
                Start              After Previous
                Canvas Group       None            ← empty = this GameObject
                Duration           0.25
                Delay              0
                Use Custom Curve   ☐
                Ease               Out Quad
                Use From           ✔
                From   [Absolute]  0
                To     [Absolute]  1

            ▼ with  Scale  0.25s  Out Back
                Type               Scale
                Start              With Previous   ← parallel with the fade above
                Rect Transform     None
                Duration           0.25
                Delay              0
                Use Custom Curve   ☐
                Ease               Out Back
                Use From           ✔
                From   [Absolute]  X 0.8  Y 0.8  Z 0.8
                To     [Baseline]  X 0    Y 0    Z 0    ← lands on the authored scale
        Loops                      1
        Loop Type                  Restart
        Play At Custom FPS         ☐
        Apply From Values Immediately  ✔
        Interrupt Others           ✔
        On Complete                (UnityEvent)
```

```csharp
private void Awake()    => anim.ApplyFromState("Show");
private void OnEnable() => anim.Play("Show");
public  void Close()    => anim.Play("Hide", () => gameObject.SetActive(false));
```

The `To [Baseline] (0,0,0)` on the scale step is the important part — `Baseline` means "the resting value captured at Awake, plus this offset", so a zero offset lands exactly on whatever scale you laid out in the scene.

---

## Example — fullscreen shader `_Progress`

Fullscreen `Image` → assign your transition material → **Add Component → UI Material Instance**.

```
UI Animation Player
  Use Unscaled Time                ✔
  Kill On Disable                  ✔
  Animations                       1
    ▼ TransitionOut
        Name                       TransitionOut
        ▼ Steps                    1
            ▼ AFTER  Overlay (self) (Material Float)  0.8s  In Out Quad
                Type               Material Float
                Start              After Previous
                Material Inst.     None            ← empty = this GameObject
                Shader Property    _Progress
                Duration           0.8
                Delay              0
                Use Custom Curve   ☐
                Ease               In Out Quad
                Use From           ✔
                From   [Absolute]  0
                To     [Absolute]  1
        Loops                      1
        Loop Type                  Restart
        Play At Custom FPS         ☐
        Apply From Values Immediately  ✔
        Interrupt Others           ✔
        On Complete                (UnityEvent)
```

```csharp
transition.Play("TransitionOut", () => {
    // your scene loading here
});
```

**Shader Property must not be blank** and must exist on the material's shader. If it's missing or misspelled you get one clear warning at startup naming the shader and the property, and the step is skipped rather than spamming errors every frame.

---

## Gotchas

**Unity's `+` button does not run C# field initialisers.** A freshly added animation or step arrives zero-filled — `Loops 0`, `Duration 0`, `Ease Unset`, `Shader Property ""` — not with the defaults declared in code. The player's `OnValidate` patches this: it fills in any field still sitting at its zero value, and never touches a field you've already set. Two consequences worth knowing:

- **`Loops` means play count.** `1` = play once (normal), `2` = play twice, `-1` = loop forever. `0` is meaningless and is treated as `1`.
- If you genuinely want a `Duration` of `0`, it'll be bumped to `0.25`. Use `0.01` for an effectively instant tween, or a `SetActive` step.

Tip: once one step exists, `+` **duplicates the last step** rather than creating a blank one, which is usually what you want anyway.

**Every field has a tooltip.** Hover any label in a step for an explanation of what it does.

**If you extend the tool: enum values are written out as explicit numbers, and those numbers are the contract.** Unity serializes an enum field as its integer, not its name, so what your scenes and prefabs actually store is the number. Reordering the lines in `UIAnimationStepType` is therefore safe, but *changing* a number — or reusing a retired one — silently repoints every step already authored against it, with no error and no warning. New step types take the next free number. This list was alphabetised once while the numbers were still implicit, which turned 16 authored `Scale` steps into `GraphicAlpha` steps and faded a menu to invisible.

---

## Reuse

In rough order of how much you're sharing:

- **Right-click copy/paste** of a single animation or step, across objects — see [Copying animations and steps](#copying-animations-and-steps). Makes a copy; edits don't propagate.
- **Copy Component / Paste Component Values** to move a whole configured player to another element.
- **A UI Animation Set** when the same animation is on enough objects that retuning them by hand stops being reasonable — one authored copy, many players, and per-object override by name. See [Sharing animations](#sharing-animations-between-objects).
- Prefab variants for anything genuinely shared as a whole object.

What makes any of these work is portable authoring: leave targets empty (= the player's own GameObject) and reach everything else by **Target Path** — `Panel/Icon` down, `..` up — and the animation stops caring which object it's on.
