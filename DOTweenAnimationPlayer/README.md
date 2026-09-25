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

The **Type** dropdown is split into sections — Transform, Punch & Shake, Color & Fade, Material, Other — each alphabetical, so a new type always lands in a sensible place. Every other step in a list is shaded, like spreadsheet rows, so you can see where one expanded step ends and the next begins.

Each step's collapsed header reads like a timeline line: `AFTER   Logo (Scale)   0.25s   Out Back` — start mode, the object it drives, the property in parentheses, duration, ease. A step that drives the player's own GameObject says `[self]` rather than repeating the name at the top of the Inspector. When it doesn't fit, a Target Path gives way from its front (`…/HoverHighlight`), since the end of a path is what names the object; otherwise the end is cut. Hover a shortened header for the full text. A ⚠ at the end of a header means the step will do nothing as authored — see [Type check](#type-check).

### Step types

| Section | Types |
|---|---|
| Transform | `AnchoredPosition` · `LocalPosition` · `OffsetMax` · `OffsetMin` · `Rotation` · `Scale` · `SizeDelta` |
| Punch & Shake | `PunchAnchoredPosition` · `PunchRotation` · `PunchScale` · `ShakeAnchoredPosition` · `ShakeRotation` · `ShakeScale` |
| Color & Fade | `CanvasGroupAlpha` · `GraphicAlpha` · `GraphicColor` |
| Material | `MaterialColor` · `MaterialFloat` |
| Other | `PlaySound` · `SetActive` |

`SetActive` and `PlaySound` are **instant** — they happen at a point in the timeline rather than over one, so they show a `Delay` but no `Duration` and no easing.

The position and size steps can also follow a curved or zig-zag route to `To` instead of a straight line — see [Movement paths](#movement-paths).

### Punch and shake

These jolt a property and let it settle back to where it started, so they have no FROM/TO and no Ease — DOTween drives the oscillation itself. They show:

| Field | Punch | Shake |
|---|---|---|
| `Punch` / `Strength` | How far it jolts, per axis | How far it shakes — one number for `ShakeAnchoredPosition`, per axis for `ShakeRotation` / `ShakeScale` |
| `Vibrato` | How many times it oscillates over its Duration | same |
| `Elasticity` / `Randomness` | How far it may overshoot past its start (0–1) | How random the direction is, in degrees |

**On UI, rotate around Z only.** `PunchRotation` and `ShakeRotation` take a strength per axis so you can leave X and Y at `0` — turning around those tips a flat element over in 3D. `(0, 0, 15)` is a firm shake. `ShakeScale` takes one per axis for the same reason: `(0.25, 0.25, 0)` wobbles without distorting.

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

All three new types take X/Y (Z is unused) and support [Snapping](#snapping).

---

## Targets

Each step shows exactly one target slot, chosen by its type.

**Leave it empty to target the GameObject the player is on.** Drag in a child or sibling to target something else.

Below the slot is an optional **Target Path**, and between them that's the whole system:

| Filled in | What the step drives |
|---|---|
| The target slot | Exactly that object. Wins over everything below — the path isn't even looked up, and it's greyed out in the Inspector while the slot is filled. |
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

Paths are resolved **once**, at `Awake`, not per frame. A path that matches nothing logs **one** warning naming the player and the path, and the step is skipped (a `Play Sound` step falls back to the shared source instead, since its slot is optional anyway). It is deliberately not treated as "fall back to the player" — a mistyped `Panel/Icon` that silently scaled the whole panel would be far harder to spot than a step that visibly does nothing.

An animation whose steps use only empty slots and paths is **portable**: it works on any object with the right children, which is what makes it worth sharing.

- `GraphicColor` / `GraphicAlpha` take a **Graphic**, which covers `Image`, `RawImage`, legacy `Text` **and TextMeshProUGUI**. There is no separate TMP step type.
- `AnchoredPosition`, `PunchAnchoredPosition`, `SizeDelta`, `OffsetMin` and `OffsetMax` show an X/Y field — Z is not used.
- `PlaySound` takes an **AudioSource**, and empty means something slightly different — see [Sound](#sound).
- If a target is missing at play time the step is skipped with a console warning naming the animation and step index. It won't throw.

### Type check

The Inspector checks every step against what it will actually drive, so a broken step shows up while you author it instead of as a Console warning after `Play`. A step that will do nothing gets a ⚠ at the end of its header, its header text fades, and a warning row appears under its target:

| Warning | Meaning |
|---|---|
| `'Box' has no Canvas Group` (or Graphic, RectTransform, UI Material Instance) | The Type needs a component the target doesn't have |
| `Target Path "Iconn" matches nothing` | A typo, or the object was renamed or moved |
| `Shader 'UI/Default' has no property '_Nope'` | A material step's Shader Property isn't on that material |
| `No Clip` | A Play Sound step with nothing to play |

Everything under the warning is **greyed out** until the step is fixed — Duration, Ease, From / To and the rest only matter once the step can reach what it drives. What fixes it stays editable: Type, Start, the target slot and Target Path above the warning, and a sound step's Clip or a material step's Shader Property below it. The one warning that greys nothing is a sound whose Target Path misses, because that still plays, on the shared audio source.

It follows playback's own rules exactly — the slot, then the Target Path, then the player's own object — so it checks the object at the end of a Target Path too, `..` included. In a shared set there's no scene to check against until you set **Preview On**; until then only the clip and shader-name checks run. A long warning wraps onto as many lines as it needs, so the whole message is always readable, at any Inspector width.

---

## Sequential vs parallel

Every step has a **Start** button — click it to switch between the two:

- `AFTER PREVIOUS` (`AfterPrevious`) → `Sequence.Append` — runs after everything before it.
- `WITH PREVIOUS` (`WithPrevious`) → `Sequence.Join` — runs alongside the previous step.

Per-step **Delay** works with both. Read the step list top to bottom and that's your timeline.

---

## Easing

Each step uses one of two easing sources:

- **Ease** — a DOTween preset. `Out*` eases decelerate into the end value and suit most UI.
- **Custom Curve** — the first entry in the same Ease dropdown. Pick it and a **Curve** row appears under Ease for an `AnimationCurve`; pick a preset again to switch back. The preset you had is remembered underneath, and so is the curve.

For the curve, time runs 0→1 left to right, and value `0` = the FROM value, `1` = the TO value. Going above 1 or below 0 overshoots, which is how you build a bounce or an anticipation dip.

Unity's curve editor has a **preset bar along the bottom** — click a swatch to apply a shape, or use the arrow at the right of the bar to save your own. That's a native Unity feature, so curve shapes are reusable without any extra asset.

Punch and shake steps ignore both — DOTween drives their oscillation internally, so there's no ease field on them.

Neither is changed by the [mirror commands](#mirroring-an-animation) — a mirrored `Hide` keeps the ease its `Show` was authored with.

---

## Frame rate

By default an animation moves smoothly, changing a little every rendered frame. Tick **Play At Custom FPS** on an animation and a frame-rate box appears beside the tick — from then on that animation advances in discrete steps, for a stop-motion or flipbook look.

```
Loops                      1
Loop Type                  Restart
Play At Custom FPS         ✔ [ 12 ]
```

In code the box is still the separate `FPS` field.

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

## Snapping

**Snapping** rounds positions and sizes to whole units each frame — pixel-perfect movement for pixel art. It causes visible stepping on a slow move otherwise, which is the point of it. Scale and rotation are never snapped; DOTween has no option for them.

It's one button on the animation, like a step's FROM / TO button. Click it to cycle:

```
Play At Custom FPS         ☐
Snapping                   [ DISABLED ]   →   [ ALL STEPS ]   →   [ PER STEP ]
```

| Setting | What snaps |
|---|---|
| `DISABLED` | Nothing — except a step whose own Snapping box is ticked (see below) |
| `ALL STEPS` | Every step that moves or resizes something. Movement paths too |
| `PER STEP` | You choose: every position or size step shows its own **Snapping** box |

A step whose own box is ticked **always** snaps, and its box stays visible whatever the button says, so a setting that changes playback is never hidden. That's also how animations authored before the animation-wide setting existed keep working untouched: their steps still carry their own ticks. In code it's still two bools, `Snapping` and `SnapPerStep`.

---

## FROM / TO

Each endpoint has a **mode**:

| Mode | Meaning |
|---|---|
| `Absolute` | Use the value exactly as typed. |
| `Baseline` | The element's resting value, captured at `Awake`, **plus** the typed value as an offset. |
| `Current` | Whatever the value is when the tween starts, plus the typed value (DOTween relative). Only available on **To**, and only when there is no From. |

Hover a mode dropdown for what its options mean — each one describes only the options it offers. Hover the `To` label for what the row is.

The **From** dropdown doesn't offer `Current`. Older data could set it there, where it never meant "an offset from the start": it makes the step ignore the From value entirely, the same as the button reading `TO`. A step that already has it keeps showing it, with a tooltip that says so, and nothing is rewritten.

**Use `To: Baseline` for anything that should land on its authored resting state.** Ten `Show`s in a row all land on exactly the same value, and if you later change the resting scale/position in the scene the animation follows automatically. This is what stops repeated Show/Hide from drifting.

The **FROM / TO** button at the start of the first endpoint row switches the FROM endpoint on and off (the field is still called `UseFrom` in code). It reads `TO` by default: one row, and the step travels to it from wherever the property already is. Click it and it reads `FROM`: that row becomes the starting value and a `To` row appears under it. It's the same control DOTween's own animation component uses, and it saves a row per step. **Apply From Values Immediately** (on by default, per animation) snaps every FROM value the moment the animation starts rather than when each step begins — this is what prevents an element flashing at full opacity through a delayed step before jumping to 0.

Punch and shake steps have no FROM/TO — they show `Punch` / `Strength` instead, and ignore Ease (they carry their own). See [Punch and shake](#punch-and-shake).

### Use Current Value

The **record** button at the end of every From and To row copies what the step's target holds right now into that row — drag an object where you want it, click, and the step lands there. It works for positions, sizes, offsets, scale, rotation, alpha, colours and material values.

What it stores depends on the row's mode, so the step always ends up exactly where the object was:

| Mode | Stored |
|---|---|
| `Absolute` | The value as it is |
| `Baseline` | The difference from the resting value |
| `Current` | The difference from where the step starts |

**Pose inside a preview.** Out of a preview, wherever the object sits *is* its resting value — so `Baseline` and `Current` would always store 0, and those buttons are greyed out until a preview is running. The workflow is the one Unity's Animation window uses for keys:

1. Press **Reset** (or Play) on the animation.
2. Drag, resize, recolour or rotate the object. Selecting it to do that is fine: the preview keeps running while the selection is on the player or one of the objects it animates.
3. Select the player again and click the record button on the row.
4. **Stop and Restore** puts the object back. The value you copied stays.

`Absolute` works any time, but outside a preview the object stays wherever you dragged it. Rotation is read as the nearest angle to zero, so a turn to `-10` isn't copied as `350` and spun the long way round.

---

## Movement paths

By default a step moves in a straight line from its start to `To`. Set **Custom Path** (under `To`) to `Curved` or `Linear` and it travels through a list of points on the way instead — an arc for a card flying into a hand, a swoop, a zig-zag.

```
To                    Absolute   X 250    Y 150
Custom Path           Curved
Point 1               Absolute   X -50    Y 200    [-]
Point 2               Absolute   X 150    Y -100   [-]
                      [ Add Point ]  [ Edit Path in Scene ]
```

This is **not** the ease. The path is *where* the object goes; the ease is still *how fast* it gets there — it controls how far along the path the step is, so `Out Quad` still decelerates into `To`, just along the curve. Speed along the path is constant apart from the ease, so a long segment and a short one are covered at the same rate.

| Custom Path | Meaning |
|---|---|
| `Disabled` | A straight line, as without a path. The default. |
| `Curved` | A smooth curve through every point (DOTween's Catmull-Rom path). |
| `Linear` | Straight lines between the points, with a sharp corner at each. |

**Points follow `To`'s mode**, and the mode column beside each point shows which one that is. `Absolute` = as typed, `Baseline` = an offset from the resting value, `Current` = an offset from wherever the step starts. That's what makes a `To: Current` path portable — the same swoop works from wherever the object happens to be. The start of the path is the `From` value when the step has one, and otherwise wherever the object is when the step begins, exactly as without a path.

Available on `AnchoredPosition`, `LocalPosition`, `SizeDelta`, `OffsetMin`, `OffsetMax` and `Scale` — every vector step except `Rotation` (DOTween rotates through a quaternion, not through the Euler values a path would pass through) and punch/shake (no endpoint to travel to). A path through `SizeDelta` or `Scale` is real and works — grow wide, then tall — it just can't be drawn in the Scene view.

With a shape chosen but no points, the step still moves in a straight line. Setting it back to `Disabled` keeps the points, so you can switch the path off to compare without losing it. In code it's still `UseCustomPath` and `PathShape`.

### Editing a path in the Scene view

On a position step, **Edit Path in Scene** draws the path where the object will actually travel, with handles:

| Do | Get |
|---|---|
| Drag a numbered point | Moves it |
| Drag `To` (green ring) or `From` (grey ring, when the step has a From) | Moves that endpoint |
| Click a small **+** on a segment | Adds a point there |
| Ctrl+click (Cmd on Mac) a numbered point | Removes it — it turns red while Ctrl is held over it |
| `Esc`, **Done**, or the button again | Stops editing |

Points move in the canvas plane, so a drag can't push one off the canvas in depth even in a perspective Scene view. Every drag is an ordinary Undo step, and it marks prefab overrides exactly as typing into the Inspector would. While editing, the move tool is hidden and clicks on empty space are ignored — the same way Unity's own *Edit Collider* works — so a missed handle can't move the object or select something else.

Things worth knowing:

- **The path is drawn from where the step really starts.** A step without a From starts wherever the steps before it leave the object, and the editor works that out by replaying the animation from rest — so a path in the *second* step is drawn from where step one ends. It stays put while a preview plays, too: it's drawn from the resting state the preview captured, not from wherever the object has got to. What it can't know is where a *different* animation left things, so a step relying on that is drawn as if played from rest.
- Only position steps can be edited in the Scene view, only on a player (a Shared set has no object to draw on), and not in play mode. The button disables itself and says why.
- Selecting something else, entering play mode or recompiling ends the edit.
- The drawn curve reproduces DOTween's own path maths, including its end conventions, and was checked against a real path tween: the object stays on the line.

---

## Sound

A **PlaySound** step fires a clip at its point in the timeline. Put one first in a `Press` animation and you have a button click; put one at `Delay 0.15` in a `Show` and it lands with the scale bounce.

```
▼ AFTER  ui_click (Play Sound)
    Type               Play Sound
    Start              [AFTER PREVIOUS]
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

Step 3 is the point of the whole thing — you can add a click sound to forty buttons without adding forty AudioSources. It appears in the hierarchy as **UI Animation Audio** under *DontDestroyOnLoad*, is 2D (so the AudioListener's position is irrelevant), and has `ignoreListenerPause` on. It only exists in play mode: out of it `UIAnimationAudio.Shared` is null, because `DontDestroyOnLoad` throws there *after* the object already exists, which would leave one in your open scene per call.

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
| Movement path points | Reversed, so the mirror walks the same path backwards. On a `To: Current` step they're also re-based onto the new start. A `Linear` path retraces exactly; a `Curved` one lands within a pixel or two, because DOTween shapes the two ends of a curve slightly differently |
| `SetActive` on | `SetActive` off |
| Punch / shake | Unchanged — they already return to where they started |
| `PlaySound` | Keeps its clip. If a hide needs a different sound, swap it afterwards |

Everything outside the steps — `Loops`, `Loop Type`, `Play At Custom FPS`, `Interrupt Others`, `Notes`, `On Complete` — is carried across untouched.

**Easing is deliberately not mirrored.** A strict time-reversal would turn `Out Quart` into `In Quart`, but the house style here is that things decelerate into place in *both* directions, so the ease you authored is the ease you keep. Endpoints and timing are what mirror; how the motion feels is a separate choice. Change it by hand on the mirrored copy if you want the other reading.

### The one case it can't get right

A step with no From (its button reads `TO`) has no authored start, so there is nothing exact to mirror onto — its forward starting value was whatever the property happened to hold at the time.

For those steps the mirror does the best it can: the step's button is switched to **`FROM`**, set to the forward `To` (which *is* known), and `To` becomes `Baseline + 0`, the resting value. Then it **logs a warning naming each affected step**, because that second half is a guess. Click the warning to ping the object.

That guess is right for an animation authored away from rest, and a no-op for one that already ends at rest — if a mirrored step does nothing, this is why, and the fix is to type the `To` you actually want.

A single step's delay is also left alone. Delays are flipped *within* a joined group; a lone step's delay is a gap between groups, which can't be expressed on the step itself.

A movement path has a similar limit. Its points follow `To`'s mode, and two cases change that mode: swapping a `From` and `To` authored in *different* modes, and the no-From case above when the old `To` was `Absolute` (the new `To` is `Baseline`). Converting the points between the two needs the resting value, which only exists at runtime, so the points are reversed but **read in a different space** — and the mirror logs a second warning naming those steps.

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

**Local animations win.** A player plays its own list first and then everything from the set whose name it hasn't already used, so one panel can override just the `Show` out of a shared set while still getting the shared `Hide`, `Press` and `Attention`. That override is the point of the feature, and both sides say when it happens — the player's inspector lists the names it overrides, and the asset's inspector warns when its Preview On player shadows one — because otherwise you can spend ten minutes tuning a curve that never plays.

This is opt-in and it is not the default. Authoring straight onto the player is fewer clicks and is right for anything only one object does.

### What an asset can't hold

**A ScriptableObject cannot reference a scene object.** So the direct target slots are disabled when you author inside a set, with the reason shown under them. Use **Target Path** for anything relative — a child, or `..` for the parent — or leave the slot empty for the player's own GameObject. Those are enough to write a genuinely reusable animation, and needing them is what keeps a shared animation honest about being shared.

If a step arrives with a target anyway — pasting one copied off a player carries live references — the asset clears it and says so, rather than letting it serialize to null at the next save with nothing said.

### Previewing a set

An animation set has nothing of its own to animate, so its inspector borrows a scene player: drop one into **Preview On** and the Play / Reset / Stop buttons run the ordinary player preview, with the same capture, restore, Undo entry and one-at-a-time rule. The player has to actually have the set in its Shared slot — it plays what its own merge produced, not what any asset happens to contain. With the slot empty, or holding a player that doesn't use the set, no buttons are drawn and a message says why.

The Preview On slot is not saved into the asset. It couldn't be: a scene reference is the one thing an asset cannot keep, which is what the whole feature is working around. It is remembered per asset until Unity recompiles or restarts, so selecting something else and coming back finds it still set.

### Things worth knowing

- Each player takes its **own copy** of the set's animations at `Awake`. It has to: resolved targets and the live sequence are per-object state, so two players sharing one asset would otherwise animate each other's objects. The cost is that **editing the set at runtime does not reach players that have already started** — and that assigning `Shared` from code after `Awake` does nothing.
- Right-click copy/paste and the mirror commands work on a set exactly as they do on a player, so an animation can move between the two in either direction.
- Two animations with the same name inside one set: the first wins, the second is unreachable, and the asset warns.

---

## Previewing

The Inspector has **Play / Reset** buttons per animation, and they work **without entering play mode**. The list includes animations from the Shared set as well as local ones. Every button has a tooltip.

| Button | Does |
|---|---|
| `Play` | Runs the animation on the real scene objects from its first frame — what `Reset` shows — carrying on from where the last animation ends |
| `Reset` | Jumps to the animation's first frame without playing it: steps with a From snap to it, the rest stay put. `Play` straight after runs from that frame |
| `Stop and Restore` | Ends the preview and puts every value back as it was before the first `Play` |

**A preview chains.** Play `OpenCredits`, then `CloseCredits`, and the close starts from where the open ends — which is the only way to judge a close animation, since its whole job is to start from the open state. If the open is **still playing** when you press the close, it's finished first, so where the close starts never depends on when you clicked. Pressing `Play` on the **same** animation again starts it over from where it started last time, so iterating on one animation still replays it from the top — and after `Reset`, "where it started" is the frame `Reset` showed. `Stop and Restore` goes all the way back to rest.

**Every `Play` starts from the animation's first frame**, exactly as `Reset` then `Play` would: each step with a From jumps to it at once. That matters for an animation with **Apply From Values Immediately** off — in play mode each of its delayed steps waits at its current value until the step begins, which played from rest makes a Close look broken, when in the game it only ever plays after its Open.

Two things here are deliberately not how play mode behaves: that first-frame rule, and interruptions — in play mode `Interrupt Others` stops the running animation where it stands and the next one starts from there. Check either case in play mode if it matters.

The info box under the buttons repeats the rules, and **Scene Gizmos** under it controls the Scene-view drawings — see [Scene gizmos](#scene-gizmos).

Edit-mode preview animates **real objects in your open scene**, so it takes some care:

- Values are **captured before the first `Play` and restored when the preview ends**, so a preview leaves nothing behind — including a punch or shake stopped half way through.
- The whole preview is **one Undo step** — `Ctrl+Z` is the escape hatch if something looks wrong.
- `To: Baseline` endpoints are always measured from rest, however many animations you chain, so they never drift.
- Edits made in the Inspector between two `Play`s are picked up by the second one.
- Selecting something the preview doesn't animate, **entering play mode** and a script recompile each end the preview and restore first. Entering play mode matters most: Unity backs the scene up as it stands, so an unrestored preview would come back out of play mode looking authored.
- Selecting **one of the objects the preview animates** — or the player's shared set — keeps it running, so you can select the thing you're posing, drag it, and come back to click [Use Current Value](#use-current-value). The preview ends once the selection moves anywhere else.
- Previewing a different player ends the current preview first; only one player previews at a time.
- **`Play Sound` steps are skipped** for the whole preview, and **`On Complete` events do not fire** — an `On Complete` is a UnityEvent wired to arbitrary game code, and a preview has no business running that outside play mode.

In play mode the buttons just call the ordinary runtime API, so sound and `On Complete` behave normally.

---

## Scene gizmos

With a player selected, the Scene view draws what its animations do:

- **Position steps** — `AnchoredPosition` and `LocalPosition`, paths included — as a line from start to end with arrows showing which way the object travels: a ring where it starts, a dot where it ends.
- **Size steps** — `SizeDelta`, `OffsetMin`, `OffsetMax` and `Scale` — as the element's outline at evenly spaced moments of the step, fading in from the first to the last.

Rotation, colour, fades, material values, punch/shake, SetActive and sound aren't drawn. A step that goes nowhere isn't drawn either.

The **Scene Gizmos** button under the preview opens the settings:

| Setting | Options |
|---|---|
| Show | `Disabled` · `All Animations` · `Expanded Animations` (the default) · `Expanded Steps` — "expanded" means open in the Inspector, so what you're editing is what you see |
| Colors | `Distinct` — a different colour per drawing, spread as far apart as possible, with **Shuffle Colors** for a new set · `Rainbow` — red for the first drawing through to purple for the last, in timeline order. Rainbow is for `Expanded Steps` only: in the other modes the row is greyed out and `Distinct` is used, since across whole animations a gradient says nothing a distinct colour doesn't say better. Your choice is kept for when you switch back |
| Outlines | How many outlines a size step is drawn with, 2 to 12 |

The settings are your own editor preferences — they're never saved into a scene, prefab or asset. A shared set draws on its **Preview On** player. Gizmos are edit-mode only.

Where each step starts comes from replaying the animation from rest, the way the preview plays it, so a step without a From is drawn from where the steps before it leave the object — and the drawings hold still while a preview moves the real objects around underneath them. It was checked against real playback: every drawn moment lands within 0.07 units of where the object really is at that moment, snapping and Play At Custom FPS included. Like the path editor, it can't know where a *different* animation left things: `All Animations` draws each one as if played from rest, so a Close that relies on its Open is drawn from rest.

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

In play mode the inspector shows **Play / Reset / Stop** buttons per animation so you can tune timing without a test script. `Reset` snaps to the FROM values without playing — it's `ApplyFromState`.

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
            ▼ AFTER  [self] (Canvas Group Alpha)  0.25s  Out Quad
                Type               Canvas Group Alpha
                Start              [AFTER PREVIOUS]
                Canvas Group       None            ← empty = this GameObject
                Duration           0.25
                Delay              0
                Ease               Out Quad
                FROM   [Absolute]  0
                To     [Absolute]  1

            ▼ with  Scale  0.25s  Out Back
                Type               Scale
                Start              [WITH PREVIOUS]   ← parallel with the fade above
                Rect Transform     None
                Duration           0.25
                Delay              0
                Ease               Out Back
                FROM   [Absolute]  X 0.8  Y 0.8  Z 0.8
                To     [Baseline]  X 0    Y 0    Z 0    ← lands on the authored scale
        Loops                      1
        Loop Type                  Restart
        Play At Custom FPS         ☐
        Snapping                   [DISABLED]
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
            ▼ AFTER  [self] (Material Float)  0.8s  In Out Quad
                Type               Material Float
                Start              [AFTER PREVIOUS]
                Material Inst.     None            ← empty = this GameObject
                Shader Property    _Progress
                Duration           0.8
                Delay              0
                Ease               In Out Quad
                FROM   [Absolute]  0
                To     [Absolute]  1
        Loops                      1
        Loop Type                  Restart
        Play At Custom FPS         ☐
        Snapping                   [DISABLED]
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
