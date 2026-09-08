# Flipnote Style Animation

Sprite-frame flipbooks for UGUI `Image`s. No Animator Controllers, no Animation Clips,
everything authored directly on the component.

| File | What it is |
| --- | --- |
| `UIFlipbookClip.cs` | Serializable data: frames, FPS, loop mode, timing mode |
| `UIFlipbookClipAsset.cs` | Optional: the same data saved as a shared project asset |
| `UISpriteFlipbook.cs` | Plays a clip onto an `Image` |
| `UIFlipbookSelectable.cs` | Picks the clip for a `Selectable`'s interaction state |
| `Editor/` | Preview, the clip drawer, and the right-click frame commands |

**This is completely separate from `DOTweenAnimationPlayer/`.** It only ever writes
`Image.sprite`. DOTween can move, scale, rotate and fade the same object at the same
time with no conflict — that is the intended split.

## Dropping this into a new project

Copy the whole folder, **including the `.meta` files** — they carry the script GUIDs, and
without them a prefab built in one project loses every component when opened in another.

Everything lives in the namespace **`rmf_claude.FlipbookAnimation`**, so any script that drives
a flipbook needs:

```csharp
using rmf_claude.FlipbookAnimation;
```

Nothing is left in the global namespace, so it cannot collide with a `UIFlipbookClip` the host
project already has.

There are no package dependencies — no DOTween, nothing. The `Editor/` subfolder name is what
puts the editor scripts in `Assembly-CSharp-Editor`. **Don't add an `.asmdef`**: nothing here
needs one, and if `DOTweenAnimationPlayer/` is also in the project, adding one there would
silently remove every DOTween UI shortcut it is built on.

## A plain flipbook

1. Add `UISpriteFlipbook` to a GameObject that has an `Image`. `Target Image` fills itself in.
2. Drag your sprites into `Clip > Frames`, in order.
3. Set `FPS` and `Loop Mode`.

`Play On Enable` is on by default, so it runs as soon as the object is enabled.

From script:

```csharp
flipbook.Play();            // the clip authored on the component, from the start
flipbook.Play(clip);        // any clip; no-op if that clip is already running
flipbook.Play(clip, true);  // ...same, but always restart
flipbook.Stop();            // stop advancing, hold the current drawing
flipbook.Restart();
flipbook.Completed += () => { ... };   // non-looping clips only
```

`Play`, `Stop` and `Restart` all take no arguments and return void, so they show up in the
UnityEvent dropdown and can be wired to a button with no glue code.

There is also an `On Completed` UnityEvent on the component, firing at the same moment as the
C# `Completed` event. `Completed` fires only when a `Once` clip runs off its last frame — never
for `Loop` or `Ping Pong`, and never when playback is replaced by another `Play()`, cut short by
`Stop()`, or interrupted by the GameObject being disabled.

## Loop modes

| Mode | What it does |
| --- | --- |
| `Once` | Play through and hold the last frame. The only mode that reports `Completed`. |
| `Loop` | Wrap back to frame 0 forever. |
| `Ping Pong` | Bounce: `A B C D C B A B C…` forever. |

Ping Pong does not hold the end frames for a double beat — it turns at `count - 2`, so a
4-frame clip reads `ABCDCBABCDCB`, not `ABCDDCBAABCD`.

## Timing

`Timing` picks how long each drawing is held.

**`Constant FPS`** — every frame holds for `1 / FPS`. The default and usually the right answer.

The FPS control is a slider from 1 to 12 next to a plain number field. The slider covers the
hand-drawn range; **the field accepts anything above it**, so type `24` if you want it, and the
slider just pins at 12. (Unity's `[Range]` cannot do this — it clamps typed input too — so the
two halves are drawn by hand.)

**`Random Offset`** — `1 / FPS` plus a fresh random number from `Random Offset Min … Max`,
in seconds. `Min` may be negative; the result is floored so a frame can never take zero time.
A range of `0 … 0` is identical to `Constant FPS`.

This is the low-effort way to stop a flipbook sounding metronomic. The offsets are re-rolled
**every pass**, so each time through the drawing is timed slightly differently, and the roll
lives on the *player*, not the clip — several objects sharing one clip asset all get their own
numbers.

**`Per Frame Durations`** — hand-typed seconds, matched to `Frames` by index:

```
Frames:          [ pose_a, pose_b, pose_c ]
FPS:             12
Frame Durations: [ 0.5,    0,      0      ]
```

`pose_a` holds half a second; entries left at `0`, and any frame past the end of the list, fall
back to the FPS time — so you only fill in what you want to change. Note that this list is
matched **by index**, so inserting a frame in the middle shifts every hold after it.

## Right-click on a frame list

Right-click the `Frames` list, one of its entries, or the clip header:

| Command | What it does |
| --- | --- |
| `Sort Frames By Name` | Numeric-aware, so `frame_2` sorts before `frame_10`. Empty slots go last. |
| `Reverse Frames` | Plays the drawing backwards. This is how you build a Hide from a Show. |
| `Clear Frames` | Empties the list. |

The sort compares runs of digits as numbers and ignores leading zeros, so `frame_007` and
`frame_7` land together. This exists because dragging a dozen sprites in is the actual cost of
authoring a flipbook, and Unity's multi-drag order is not dependable.

## Previewing

Both components have a preview panel that works **without entering play mode**. `UISpriteFlipbook`
gets Play / Stop / a frame scrubber; `UIFlipbookSelectable` gets a button per state, showing the
clip that state would really play — fallback chain included, so an empty `Highlighted` correctly
previews `Normal`.

A preview animates the real `Image` in your scene. Three things make that safe:

- the sprite is captured before anything moves and written back when the preview stops;
- the whole preview is one `Undo` step, as a backstop;
- `On Completed` events are suppressed, so nothing in the animation can run game code outside
  play mode.

Only one preview runs at a time — starting a second stops the first, which is what guarantees the
captured sprite always belongs to the object being restored. It also stops on its own when you
select something else, enter play mode, or trigger a script reload. **Stop it before you save**
anyway; the inspector warning turns amber while one is live.

Dragging the scrubber pauses auto-advance so you can look at one drawing; `Resume` picks up again.

## Shared clips (optional)

Every clip has a `Shared` slot. Point it at a `UIFlipbookClipAsset`
(`Create > Flipbook > Clip`) and the clip's own frames and timing are ignored — everything using
that asset animates from one place.

This is **opt-in and not the default**: authoring frames on the component is fewer clicks and is
right for a one-off. Reach for an asset when the same drawing is on enough objects that retiming
it by hand stops being reasonable.

Playback state stays per-object, so sharing frames does *not* make everything animate in lockstep.
Turn on `Random Start Frame` if you want them scattered — that setting lives on the player exactly
so a shared clip can still look like many separate drawings.

A clip asset cannot point at another clip asset; `OnValidate` clears the slot and says so.

## A flipbook Selectable

1. Keep your normal `Button`. Set its **Transition to `None`** (or `Color Tint`).
   *Do not use `Sprite Swap`* — it writes `Image.sprite` itself and will fight the flipbook.
   `UIFlipbookSelectable` warns about this in `OnValidate`, i.e. while you are looking at it.
2. Add `UISpriteFlipbook` and turn **`Play On Enable` off** (the Selectable drives it).
3. Add `UIFlipbookSelectable`. `Target` and `Flipbook` fill themselves in — `Flipbook` is looked
   up on this object first, then in children, so the visual can be a child Image.
4. Fill in the state clips.

`onClick`, navigation and every other Selectable behaviour are untouched. `UIFlipbookSelectable`
implements the standard EventSystem handler interfaces and sits *beside* the Selectable — the
EventSystem delivers each event to every handler on the object, so both receive it.

Mouse hover/press works, and keyboard/controller works: `ISelectHandler` drives the Selected state
and `ISubmitHandler` flashes the Pressed clip for `Submit Press Duration` (0.1 s), since a
controller submit has no press-and-hold to read.

It takes a `Selectable`, not a `Button`, so a Toggle or a Slider handle works too.

## State priority

Exactly the order `UnityEngine.UI.Selectable` uses internally, so the flipbook and the
Selectable's own transition never disagree:

```
Disabled  >  Pressed  >  Selected  >  Highlighted  >  Normal
```

**Selected outranks Highlighted** — a focused button that you also hover shows Selected. That is
Unity's behaviour, not something added here.

A state with no frames falls through:

| State | Falls back to |
| --- | --- |
| Disabled | Normal |
| Pressed | Highlighted → Normal |
| Selected | **Normal** |
| Highlighted | Normal |

Selected deliberately does *not* fall back to Highlighted. A button keeps EventSystem selection
after you click it, so borrowing the hover look while the mouse is somewhere else reads as a stuck
button. Leaving `Selected` empty therefore means "no focus look", which is usually what you want on
a mouse-only jam build.

Entering a state always restarts its clip, and the flipbook holds exactly one clip at a time —
`Hover → Pressed → Hover` can never leave two loops running.

## Enabled / disabled

- Disabling the GameObject stops `Update`, so nothing ticks.
- Re-enabling **restarts** the clip rather than resuming mid-frame, so it is always predictable.
- A clip that simply **ran out** replays when the object is switched off and on again. A one-shot
  flourish on a panel therefore plays every time the panel is shown.
- After an explicit `Stop()`, re-enabling stays stopped. `Stop()` is a decision and it survives;
  running out is not. Calling `Play()` clears it again.
- `UIFlipbookSelectable` re-reads hover/press/selection on enable and force-applies the right
  state, because no exit event arrives while a component is off.
- If the `Image` is destroyed underneath a running flipbook, it stops instead of throwing.

## Paused / unscaled time

`Use Unscaled Time` is **on by default** on both components, so flipbooks keep running while
`Time.timeScale == 0`. That is the right default for pause menus — hand-drawn UI that freezes
when you pause looks broken.

Turn it off per component for a flipbook that is part of the gameplay layer and *should* freeze
with the world (an in-world sign, a diegetic HUD element that stops with hit-stop).

`UIFlipbookSelectable` has its own `Use Unscaled Time`, which only times the Submit press flash.
Set it to match its flipbook.

## Costs

- No per-frame allocations. No coroutines, no LINQ, no `params` arrays on the state path. The
  random timing buffer is allocated once and refilled in place.
- `Image.sprite` is only written when the frame actually changes, so the canvas is not re-dirtied
  every tick.
- `UIFlipbookSelectable.Update` recomputes its state every frame (a handful of bool reads). It has
  to: `Selectable.interactable` can be set from code with no event to hook, so polling is the only
  way the Disabled state can work at all. The clip only switches when the resolved state changed.

## Gotchas

**Unity does not run C# field initialisers for elements added with `+` on a serialized `List<T>`.**
A new element arrives zero-filled, so `FPS = 12f` and `LoopMode = Loop` would silently not apply.
`UIFlipbookClip.FillUnsetDefaults()` fixes that up from the owner's `OnValidate`, and only ever
writes fields still at their zero value. The hidden `defaultsFilled` flag is what distinguishes
"never authored" from "deliberately set to zero".

**Serialized enums are stored as integers, and those integers are the contract.**
`UIFlipbookLoopMode`, `UIFlipbookTiming` and `UIFlipbookState` assign their numbers explicitly, so
reordering the lines is harmless. *Changing* a number, or reusing a retired one, silently repoints
every clip already authored against it — with no error and no warning.

**`LoopMode` replaced a plain `bool Loop`, and Unity cannot convert one to the other.** A clip
authored under the old schema would come back as `Once`. `MigrateLoop()` reads the legacy bool once
and uses `defaultsFilled` to tell old data from a brand new zero-filled element. Both hidden fields
have to stay for that to keep working on any project still holding pre-migration assets.
