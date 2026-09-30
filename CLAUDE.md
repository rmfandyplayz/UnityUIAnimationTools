# CLAUDE.md

**Keep this file current.** When a session changes something described here — a convention, a tool, a gotcha — update it in the same session, and add an entry for anything non-obvious learned the hard way. Prune entries that stop being true. Be concise: the rule and why, and history only when the history is the warning.

*(If copying these tools into a project that already has a CLAUDE.md, merge these sections into it.)*

## The UI tools in this project

Three independent UGUI tools, each a self-contained folder with its own `README.md`. **Read it before changing a tool** (for `DOTweenAnimationPlayer`, read the wiki). No tool references another.

| Folder | What it does | Namespace |
| --- | --- | --- |
| `DOTweenAnimationPlayer/` | Inspector-authored DOTween animations, played by name | `rmf_claude.DOTweenUI` |
| `FlipbookAnimation/` | Sprite-frame flipbooks on UGUI `Image`s | `rmf_claude.FlipbookAnimation` |
| `SelectableEvents/` | UnityEvents for Selectable pointer/selection callbacks | none — global |

`SelectableEvents` is the author's hand-written code: don't restructure, namespace or modernise it unasked.

Common to all: runtime types are `public`, editor drawers and editors `internal`. **The `.meta` files carry the script GUIDs — a folder copied without them loses every component on existing prefabs.** If Animation Sets "break" after copying the folder anywhere, compare script GUIDs first (the jam's copy once had three that had drifted from the library's; they match now).

### DOTweenAnimationPlayer's docs are the GitHub wiki

`https://github.com/rmfandyplayz/UnityUIAnimationTools.wiki.git`, pushable with the author's git login. The README holds only an install note, the Code API signature block and a link. **A public API change updates both the README's block and the wiki's `Code-API.md`**; any user-visible behaviour change goes into the relevant wiki page.

Wiki conventions: plain language for non-programmers first; technical detail in a collapsed `<details><summary><b>Under the hood</b></summary>` (blank line after `</summary>`, or its markdown doesn't render); GitHub alerts (`> [!TIP]`, `> [!WARNING]`, `> [!NOTE]`); no `# Title` line; step types by Inspector name (`Anchored Position`); links as `[text](Page-Name#heading-slug)`, each checked to resolve before pushing. `_Sidebar.md` has six sections (Installation, Code API, Basic usage, Advanced usage, Nitty-gritties, Warnings and Gotchas) under a "UI Animation Player" heading, leaving room for the other tools.

### DOTweenAnimationPlayer is a UPM package

The other two are still loose folders. Projects install from the **`release` branch** — `https://github.com/rmfandyplayz/UnityUIAnimationTools.git?path=/DOTweenAnimationPlayer#release` — and update with Package Manager's **Update** button. The author chose a branch over tags: one-click updates, no need for old versions.

**Publishing is the author's job**: merging `main` into `release` through a PR on GitHub, per [`RELEASING.md`](RELEASING.md). What it asks of a session:

- **Bump `version` in `package.json` with the first change to the package after each release.** If it still equals `git show origin/release:DOTweenAnimationPlayer/package.json`, bump it in the same commit: patch for fixes, minor for additions, major for anything breaking saved data or the public API. The author doesn't bump it.
- **Never commit to or rewrite `release`.** It only receives merge commits from `main`. A squash merge would leave the same changes under different commits and the next merge conflicting with itself.
- **Commit every new file's and folder's `.meta`.** Unity ignores a meta-less asset in a read-only git package, so a missing one only shows up in projects that install it.
- The install URL never changes, so a release needs no README or wiki edit.

The Update button (6000.3's `GitUpdateAction`) re-installs the package's git URL, ignoring the hash locked in `packages-lock.json`, so it fetches whatever `#release` points at now; the refresh button ignores git packages. Tested end to end on P2. Two traps: an Editor in the background downloads the new version but only switches when the project is reopened, and **calling `Client.Resolve()` from `eval` hung the Editor** on a modal for 7+ minutes until killed. Reopen the project instead.

What the package needs, and why:

- **Two asmdefs**: `rmf_claude.DOTweenUI` at the root, `rmf_claude.DOTweenUI.Editor` in `Editor/` (Editor platform only). A package's scripts only compile inside an asmdef, where a folder named `Editor` is no longer special, so the second asmdef is what keeps editor code out of builds. Both `autoReferenced`, so `Assembly-CSharp` sees the tool unchanged.
- **Both reference `DOTween.Modules`, which exists only after DOTween Utility Panel → Create ASMDEF.** DOTween's UI shortcuts (`DOAnchorPos`, `DOFade`, `DOColor`, `DOSizeDelta`, punch/shake anchor pos) are generated into `Assets/Plugins` and otherwise compile into `Assembly-CSharp-firstpass`, which no asmdef can reference. Forgetting the click gives exactly 7 errors (CS1061 / CS1929) in `UIAnimationStep.cs` that don't name the cause, so the README does. A setup check inside the package can't help: a package that fails to compile loads no new code. **For the same reason, never add an asmdef to a copy that isn't the package.** Swapping the 7 shortcuts for DOTween core calls was declined: `DOShakeAnchorPos` uses `SetSpecialStartupMode`, a module-only helper DOTween could rename.
- `package.json` says `"unity": "2022.3"` (`SerializedProperty.boxedValue` needs 2022.1+).
- Moving into the asmdef did not break UnityEvent listeners storing `m_TargetAssemblyTypeName: UIAnimationPlayer, Assembly-CSharp` (all 32 in the jam resolve through `UnityEventBase.FindMethod`).

`FlipbookAnimation/` and `SelectableEvents/` have no asmdef; the `Editor/` folder name separates their editor code.

---

## DOTweenAnimationPlayer

Requires **DOTween Pro**, then `Tools → Demigiant → DOTween Utility Panel → Setup DOTween…` and **Create ASMDEF**. Skipping either produces errors pointing at *this* code, not the cause.

**Layout** (the author's reorganisation; every file moved with its `.meta`): `UIAnimationPlayer.cs` and `UIMaterialInstance.cs` at the root, the two components you add. `Data/`: `UIAnimation`, `UIAnimationStep`, `UIAnimationAsset`. `Helpers/`: `UIAnimationAudio`, `UIAnimationProperty`, `UIAnimationSteppedEase`, `UIAnimationLog`. `Attributes/`: the `PropertyAttribute`s. `Editor/`: `UIAnimationTargets.cs` at its root; `Inspectors/` (player and asset editors, Shared Animations, preview), `Drawers/`, `SceneView/` (gizmos, path editor, simulation), `Menus/` (context menu, mirror, Save as Animation Set).

`UIAnimationPlayer` holds named animations, each a list of steps: `player.Play("Show")`. A step drives one property on one target — anchored position, scale, rotation, colour, alpha, size delta, offset min/max, material float/colour, `SetActive`, punch or shake (position, rotation, scale), a sound, or `CustomProperty` (any public float / int / Vector2 / Vector3 / Color / string member of any component, via `DOTween.To`). Steps run in sequence or joined to the previous one, with per-step delay, duration, ease and endpoint mode (`Absolute` / `Baseline` / `Current`). It is deliberately generic — **do not add game-specific logic.**

**Every addition is back-compatible.** It is either optional and invisible when unused, or additive: a new enum number, new fields other step types ignore, new bools whose false (what old data reads) is the old behaviour. An existing player must behave exactly as before; prove it with the play-path fingerprint (see Verifying). Optional today: a shared `UIAnimationAsset` in the `Shared` slot, a step's `TargetPath`, `Play(string, Action<UIAnimationEndReason>)`, per-animation Snapping, custom movement paths, `SetFrom` / `SetTo` / `ClearOverrides`, and Shared Animations with each player's own On Complete (a hidden `Id` on `UIAnimation`, `SharedOnComplete` on the player, `[NonSerialized] PlayerOnComplete` on the clone). Additive: `CustomProperty` (enum 20), Interrupt Others' STOP / COMPLETE and REPORT / SILENT (`CompleteInterrupted` / `CompleteSilently`; false/false is the old path). Editor-only additions (gizmos, the type check, Use Current Value) can't change playback.

**Two deliberate behaviour changes, on request:** `Stop(name, complete: true)` and `StopAll(true)` finish through `Finish`, so they now run the `SetActive` steps they used to skip; and a punch or shake that `Kill` stops part way is put back where it started.

### Things that look wrong but are intentional

#### Authoring and data

- **Reversing is an authoring operation, not a playback mode.** `Mirror Animation` / `Duplicate as Mirrored` rewrite the data once. There is no runtime `PlayReverse` and **there should not be** — having both was tried and one cut.
- **Mirroring never touches easing** (preset or custom curve): the house style decelerates into place in both directions. Ease inversion was built, then removed on request.
- **`PlayAnimation` / `StopAnimation` aren't redundant.** UnityEvent's dropdown only lists void methods with at most one argument, which excludes `Play` (returns a `Sequence`) and `Stop` (optional parameter).
- **`WithPrevious` steps are groups, not rows.** Anything reordering steps must keep each group contiguous, or it silently re-parents which steps are joined.
- **`AnchoredPosition`, `SizeDelta`, `OffsetMin` and `OffsetMax` all write one rect**, so two on one target overwrite each other. A new rect-driving step type must be added to `CaptureBaseline`, or `To: Baseline` collapses the target to zero.
- **A step's `Delay` is applied only in the insert position `BuildSequence` computes.** `BuildTween` must never `SetDelay`: DOTween adds it on top, doubling every delay. This regressed once.
- **Stepped playback ("Play At Custom FPS") quantises the time handed to the ease**, not the clock, on one grid keyed off the absolute sequence position (per-tween grids read as jitter). Punch and shake are excluded.
- `UIAnimationAudio.Shared` is the one global. For a mixer, call `UIAnimationAudio.SetShared` once.
- **Snapping resolves as `step.Snapping || (animation.Snapping && !animation.SnapPerStep)`** (`UIAnimation.SnapsEveryStep`, passed to `BuildTween` as `snapAll`). The animation-wide box adds to a step's own, which is what leaves old data unchanged with no migration; the drawer shows a ticked step box even with Snap Per Step off. Don't "simplify" it into Snap Per Step choosing between the two.
- **`ShakeRotation` / `ShakeScale` take a per-axis strength (`ToVector`); `ShakeAnchoredPosition` takes one number (`ToFloat`).** Z-only rotation must be expressible (X/Y tips a flat element over in 3D), and the older field can't move.
- **`CompleteInterrupted` defaults to true only for a never-filled animation** (`Loops == 0 && Name` empty, in `FillUnsetDefaults`), not through its initializer: Unity gives a field missing from old data its initializer, and false must mean "authored before this existed". `+` on an empty list zero-fills every field, bools included, so that branch also sets `InterruptOthers` and `ApplyFromValuesImmediately` to true. `+` on a non-empty list copies the last element.

#### Runtime

- **A player deep-clones every animation it takes from a shared `UIAnimationAsset`, for correctness.** Runtime state (`RuntimeSequence`, `RuntimeCallback`, each step's resolved targets) lives on `UIAnimation` / `UIAnimationStep`, so two players reading one asset would drive each other's objects. `CloneForRuntime` round-trips through `JsonUtility` and re-points `OnComplete` at the original `UnityEvent`. So: editing an asset at runtime doesn't reach initialised players, and assigning `Shared` after `Awake` does nothing.
- **`TargetPath` reaches upward with `..`** (`transform.Find` accepts it; segments resolve one at a time, so `../Self` is a sibling named Self). The common layout — the player on an `Animations` child driving the button above it — depends on it. Don't restrict `ResolveHost` to descendants.
- **A `TargetPath` that matches nothing resolves to null, not the owner** — a step that does nothing is easier to diagnose than one scaling the wrong object. `Resolve` warns once and sets `targetPathMissed` so `HasTarget` doesn't warn again every `Play`.
- **A filled target slot skips the Target Path entirely** — it isn't even looked up. The drawer greys the path to show it.
- **The end-callback fire-once flag is a local captured by each play's closure, never a field.** An `onEnd` that replays the same animation would otherwise let the old play's teardown fire the new play's callback. Likewise `Kill` only clears `RuntimeSequence` / `RuntimeEndCallback` if they still `ReferenceEquals` the ones it started with.
- **The player has no `Update`.** A kill it didn't issue is caught by the sequence's `OnKill`, with `PendingEndReason` (armed as `Stopped`, overwritten by `Kill`) carrying the reason. Every end path has two routes to the callback; the fire-once flag drops the second. A polling `Update` was once added on an edit-mode measurement — see the DOTween table.
- **Destroying an *enabled* player reports `Disabled`, not `Destroyed`**: `OnDisable` runs first and Unity gives no way to know a destroy is coming. No heuristics.
- **`UIAnimationEndReason.NotFound` is a reason, not a silent no-callback** — a caller is never left waiting on a mistyped name.
- **From / To set from code live in `[NonSerialized]` slots beside the authored value, one per value kind (`EndpointOverride`), never written over it.** A local animation's runtime step is the serialized one, so overwriting would show in the Inspector and leave `ClearOverrides` nothing to restore. Every playback read goes through `FromVectorValue` / `ToFloatValue` / …. The value keeps the step's mode. `SetFrom` refuses a step whose From is off. Steps are addressed by index (the README warns reordering changes them). The edit-mode preview re-clones shared animations, dropping overrides; nothing sets one in edit mode.
- **Every "end it at its end state" path goes through `Finish`, never DOTween's `Kill(complete: true)`**, which skips a Sequence's internal callbacks (measured: a `SetActive` step still ahead never runs). `Finish` calls `Complete(true)` and sets `finishing` so sounds stay quiet. SILENT nulls the sequence's `OnComplete` first, so `OnKill` reports the caller's reason. The player's own `Kill` only stops where it stands.
- **`FinishRunning` snapshots who is running before finishing any, then sweeps with a plain stop.** REPORT's `On Complete` can chain into another animation on the same player; finishing whatever is live during the loop would make chains depend on list order.
- **Replaying an animation that's already running always stops it where it stands**, whatever its own STOP / COMPLETE says (those are about the *others*). Completing first would snap a step with no From to its end, so the replay would show nothing. Questioned once, when a counter test read its half-counted number as the total; the fix was the caller keeping the real total and `SetTo`-ing it (tooltip and README say so).
- **A punch or shake that `Kill` stops part way is put back where it started** (`UIAnimationStep.SettleImpulse`). DOTween's punch and shake are relative to their start, so one stopped mid-wobble stayed displaced and the next wobbled around that (spam left a scale at 2.19x). The start is read in the nested tween's `OnStart`, which runs before any write and once per lifetime. `Kill` writes it back only when `ElapsedPercentage(false)` is strictly between 0 and 1, last step first so the earliest start wins. Target and type are pinned at build time; `BuildTween` forgets the tracked tween first, since DOTween reuses dead tween objects. `Finish` needs nothing.
- **Punch and shake capture a baseline anyway**, under the property they drive, so the preview can restore one stopped mid-oscillation.
- **`RestoreBaseline` switches on `baselineType`, the Type it was captured under**, not the current one — changing Type mid-preview otherwise wrote a scale into a position. `baselineMember` / `snapshotMember` do the same for Custom Property.
- **Every warning reads "<b>who: CONSEQUENCE IN CAPITALS.</b> details"** through `UIAnimationLog.Warn`, which is `public` because the editor assembly can't see the runtime's `internal`s. Steps say "Animation 'X' step n". The flipbook formats its own inline. A step's resolve-time warnings appear at `Awake`.

#### Movement paths

- **Built on DOTween's generic `PathPlugin`** — `DOTween.To(PathPlugin.Get(), getter, setter, new Path(...), duration)` — so one path can drive `anchoredPosition`, `sizeDelta` or `localScale`. Measured traps (1.3.030): it throws a NullReferenceException at startup without `SetTarget(transform)`; it ignores `From()`, so the getter returns the authored From; it prepends the start as a point unless the first waypoint equals it. `Rotation` is excluded (quaternion). An empty point list falls back to the ordinary tween.
- **Waypoints follow `To`'s mode** (`SetRelative` offsets every point). When a mirror changes To's mode, the points can't be converted without the runtime baseline, so `UIAnimationMirror.PathSpaceChanges` reports them instead.
- **`UIAnimationPathEditor.Evaluate` reimplements DOTween's Catmull-Rom** (`Path.GetPoint` is `internal`) with its non-textbook ends: the control point before the first point is the *second* point, and after the last it's the last mirrored through the one before. It matches a real tween within 0.001. "Fixing" the ends draws a curve the object doesn't follow. It's also why a mirrored Curved path retraces only approximately; Linear is exact.
- The path is created with a transparent gizmo colour, since DOTween would draw raw `anchoredPosition` values as world positions.
- **The Scene-view path editor follows Unity's Edit Collider pattern:** hides `Tools` (restoring it, also from `beforeAssemblyReload`, since `Tools.hidden` survives a reload), registers a passive default control so empty clicks don't deselect, writes only through a `SerializedObject`. It edits only `AnchoredPosition` / `LocalPosition` outside play mode, draws from rest plus the step's simulated start, and keeps drawing through a preview. The gizmos skip the step it is editing.

#### Custom Property steps

- **The member is stored as three plain values** — the component's `Type.FullName`, the member name, a `UIAnimationPropertyKind` — and resolved once in `Resolve`. The component is found by matching `GetType().FullName` on the object, not `Type.GetType`, so a script changing assembly doesn't break it. The kind is stored because a shared asset with no Preview On can't derive it. A type mismatch is a warning and a skipped step, never a guess. All lookup rules live in `UIAnimationProperty.cs` (`TryLocate` / `FindMember` / `CollectMembers`), shared by playback, the dropdown and the type check.
- **`FindMember` walks the type hierarchy one `DeclaredOnly` level at a time.** A flat `GetProperty` calls a `new`-redeclared name ambiguous, and a getter-only override has no setter; the walk binds the base declaration, whose delegate still dispatches to the override. It stops at `UnityEngine.Object` / `Component` / `Behaviour` / `MonoBehaviour`, which keeps `name`, `tag` and `enabled` out.
- **Accessors are bound with `Delegate.CreateDelegate`**, as UnityEvent does, so there's no per-frame reflection; fields go through `FieldInfo`. IL2CPP stripping above Minimal can remove a setter only this tool calls — the README documents `link.xml`.
- **Nothing in the Inspector calls a getter except Use Current Value's click.** A getter is arbitrary code, and everything else runs every repaint.
- **`CapturePropertyBaseline` / `RestorePropertyBaseline` catch exceptions**; no other step type does. One throwing member mustn't leave the player half-initialised or the scene half-restored. It's warned once and the step skipped.
- **A counted-number text (`NumberText`) keeps two baselines**: the parsed number (`baselineFloat`, what `Baseline` measures from) and the original string (`baselineText`, what's restored — else `Score: 120` becomes `0`). `ParseNumber` reads only a plain number (current culture, then invariant), anything else as 0. `FormatNumber` falls back to the plain number on a `FormatException`.
- **Typewriter text offers `Absolute` and `Baseline`, never `Current`**: a mirror can't take appended characters back off. It uses `SetOptions(richText: true)`, so tags never show half-written.
- **An `int` member's values live in the float fields**, drawn with an `IntField`. `FromText` / `ToText` are the only value fields the type added.
- **The edit-mode preview runs the member's real setter**, side effects and all; the README says so.

### Edit-mode preview (`Editor/Inspectors/UIAnimationPreview.cs`)

- **The lifecycle and button rows (`DrawRow` / `DrawFooter`) live there, not in an inspector**: two inspectors drive it, and a second copy would drift.
- **Within one player it chains rather than restoring between Plays** — `CloseCredits` after `OpenCredits` must start from open. A different animation pressed mid-play first finishes the running one (`Complete(true)`, firing SetActive callbacks; an endless loop is left where it stands). The preview never reaches the interrupt code (it calls `StopAll()` before `Play`); play mode only matches it under COMPLETE, and the README says so.
- **Every preview `Play` is Reset-then-Play**: `ApplyFromState`, then `EditorMarkPreviewStart` re-captures the start snapshot. Deliberately unlike play mode when `ApplyFromValuesImmediately` is off: `CloseCredits` played from rest looked broken, reported as a bug.
- **Chaining stays safe two ways.** (1) Baselines are still captured at rest before every Play: `EditorContinuePreview` snapshots the current state (through `CaptureBaseline` / `RestoreBaseline` themselves, swapped into the baseline fields, so no step type can be covered by one and missed by the other), restores rest, re-initialises there (picking up Inspector edits) and puts the snapshot back. (2) Playing the same animation again restores its start snapshot first. A snapshot is only written back if the step still resolves to the same object.
- **`EditorSuppressSound` is on for the whole preview**, since sounds fire from callbacks during playback. Out of play mode `DontDestroyOnLoad` throws after the GameObject exists, leaking a `UI Animation Audio` into the scene per call; `Shared` also returns null out of play mode.
- **It unwinds from `[InitializeOnLoad]` hooks**: `playModeStateChanged` at `ExitingEditMode` (before Unity backs up the scene) and `beforeAssemblyReload`, plus `OnDisable` and `Selection.selectionChanged`.
- **It survives selecting the objects it animates**, because Use Current's record workflow means selecting and posing them. `EndIfOwnedBy` leaves it running, owned by nobody, while the selection is on the player, its asset or anything it animates; `Selection.selectionChanged` ends it once the selection leaves. An owner that still exists (a locked inspector, a harness's `object`) ends its own. A scripted test missed this because it never changed the selection.
- **Use Current's `Baseline` and `Current` only work inside a preview**: outside one, the current value *is* rest, so both would store 0. Rotation reads wrap with `Mathf.DeltaAngle` (Rotation is `FastBeyond360`, so `350` would spin the long way).
- **Nothing in the Inspector may read `UIMaterialInstance.Material`**: the first read instances a material onto the Graphic. Read `UIAnimationTargets.SourceMaterialOf`. (The preview's material steps still use `.Material`; that predates this.)

### Inspector and editor code

- **Editor-side target resolution is `Editor/UIAnimationTargets.cs`, mirroring `Resolve` without side effects.** A shared asset's steps resolve against the asset inspector's Preview On player, kept per asset in a static map (`PreviewPlayerFor`) until the next domain reload — not a serialized field, since an asset can't hold a scene reference.
- **The player inspector draws its own fields (`DrawFields`)**, iterating like `DrawDefaultInspector`, to label `Shared` "Shared Anim. Asset" (the serialized name stays; renaming would empty saved slots) and put Shared Animations after Animations. **`UIAnimationDrawer` draws every animation**: Unity's layout plus a grey header note ("overrides shared", "overridden locally", …) or the Override button, drawn before the foldout so the foldout doesn't take the click.
- **Type and Ease dropdowns are `GenericMenu`s, not enum popups.** Type is grouped (`IsImpulse` / `TargetKindOf`) and sorted in the drawer; Ease is grouped by direction (Linear and Flash, In, Out, In Out) with the enum number as tie-break, since `List.Sort` isn't stable. The stored numbers never move for menu order. `Custom Curve` is an Ease entry setting the same `UseCustomCurve` bool. Menu callbacks write through `.Copy()`'d properties and `ApplyModifiedProperties`, since they run after the repaint.
- **Several rows are one control over several fields**, each binding `BeginProperty` to whichever field decides what's shown (so override bold and Revert follow what's visible), and nothing is rewritten until clicked: the Ease row over `Ease` / `UseCustomCurve`; the Snapping button over `Snapping` + hidden `SnapPerStep` (`SnapPerStep` wins, as at runtime); the Custom Path dropdown over `UseCustomPath` + `PathShape` (`Disabled` keeps shape and points); Start as AFTER / WITH PREVIOUS over the enum (anything but `AfterPrevious` reads WITH); Play At Custom FPS with `FPS` inline (`UIAnimationInlineValueAttribute`; the `BeginProperty` rect stops where the field starts). `UIAnimationShowIfAttribute` is unused but kept as a public type.
- **Start and Interrupt Others' buttons are sized from their words** (`CalcSize` of both labels + 8); a fixed 72 clipped `COMPLETE` at 125% scaling. Snapping's fixed 84 fits.
- **The drawer reads enums with `intValue`, not `enumValueIndex`** — only the number is the contract.
- **The mode dropdown's tooltip is an empty `GUI.Label` over the popup** (a popup doesn't show its label's tooltip), built per dropdown by `ModeTooltip` from the options that dropdown offers.
- **The From dropdown doesn't offer `Current` but still shows it when stored.** `HasAuthoredStart` excludes it, so it behaves like TO. It's never rewritten: changing what stored data means is the back-compat line. By contrast the To row with From on rewrites a stored `Current` to `Baseline` on draw — left alone, flagged here.
- **The step list's row shading comes from ReorderableList's source**: `BandLeft = 28` (drag handle 20 + foldout margin 8), `BandRight = 6`, and the drawer's rect is the whole row, so the band is at `y - 1`, `position.height` tall. `ReorderableList.Defaults` gives the constants by reflection.
- **A step the type check finds dead is greyed out below its target** (`DisabledScope`, header faded by `DimFoldout`). The fields that fix it — Start, Type, slot, Target Path, `Clip`, `ShaderProperty` — are drawn outside the scope, since nested `DisabledScope`s AND together. A sound whose path misses is warned about, not greyed: it still plays. The warning row wraps (see `GetPropertyHeight` under shared conventions).
- **IMGUI drops images** from `EditorGUI.Foldout` with the foldout style, and from a label whose text overflows, so the warning icons are separate `GUI.Label`s.
- **Save as Animation Set (`Editor/Menus/UIAnimationSetExport.cs`) turns each filled target slot into a Target Path**, since an empty slot means the player's own object. The path goes up with `..` to the nearest shared ancestor and down by name, and is kept only if `UIAnimationTargets.Resolve` without the slot returns the same object. Otherwise (another root, a same-named sibling, a second component of its type) it's written from the scene root, misses and warns at play, and is listed in the Console. On Complete listeners targeting a scene object are removed (an asset can't reference one). The player is untouched. Saving over an existing set writes into it; `CreateAsset` would change its GUID and empty every Shared slot on it. The session folder is `SessionState`, and a second menu entry saves there without a dialog. To check it: save every player in `UITester2` to `Assets/_Check`, compare each step's resolved target against the saved one's with no slot, and expect everything else's JSON to match.
- **Shared Animations (`Editor/Inspectors/UIAnimationSharedSection.cs`) draws a private copy of the set**: `Object.Instantiate` with `DontSave` — **not `HideAndDontSave`, whose `NotEditable` greys out the whole section**, Override and On Complete included. Fields that mustn't change are greyed by `UIAnimationDrawer`'s own `DisabledScope`. The copy is refreshed by `EditorUtility.CopySerialized` when `EditorUtility.GetDirtyCount(asset)` changes or a `generation` counter moves. **Saving resets the dirty count to zero**, so Add On Complete (which gives the Id and saves in one go) looked dead while hidden duplicates piled up. `generation` is bumped by `AddOnComplete` and by any import (a nested `AssetPostprocessor`), and `AddOnComplete` refuses a second entry per Id. Unity's right-click Duplicate / Delete Array Element apply themselves despite greying, so `KeepOnlyCopying` removes them (leaving Copy and Override). A static map from the copy's `SerializedObject` to its player (`PlayerOf`) tells everything else it's drawing the section: `UIAnimationTargets.OwnerOf` asks it first (so the type check runs against the viewing player), the drawer's margin cache is keyed by object, viewer and path (`MarginKey`; one path is on screen twice at different widths), and the gizmos read expansion from it (`ViewOf`). Foldouts still open inside a `DisabledScope`.
- **A player's own On Complete finds a set's animation by a hidden `Id`**, so renaming it in the set keeps it. The Id is given at the first Add On Complete On This Player, and the set is saved there and then (`SaveAssetIfDirty`) so a scene never points at an unsaved Id. A copy must not keep an Id: `UIAnimationAsset.OnValidate` clears one matching an animation above it; the clipboard's `Rebuild`, Duplicate as Mirrored and Save as Animation Set clear it (Paste over an animation, and overwriting a set, carry the old one back by name). **`boxedValue` refuses a `PersistentCall`**, so Override copies listeners value by value (`CopyValues`), bounded by property path since `GetEndProperty` on a list's last element runs past it. Entries matching nothing are listed with a Remove button. An overridden shared animation offers no Add button; an entry already on one is shown greyed as unused.
- **Gizmos draw from `UIAnimationPlayerEditor.OnSceneGUI`**, which only runs for editors the Inspector built; "Expanded" is `isExpanded` on that editor's `serializedObject`. **`OnSceneGUI` must not touch `serializedObject` or `targets`** — Unity logs an error every Scene view repaint. It uses `sceneSerialized`, taken in `OnEnable`, refreshed in `OnInspectorGUI`, null when multi-editing. Rainbow colours apply only in `Expanded Steps` (`EffectiveColors`). Harness traps: after changing selection in the background call `ActiveEditorTracker.sharedTracker.ForceRebuild()`, and `isExpanded` set through a different `SerializedObject` doesn't reach an existing editor.
- **`UIAnimationSimulation` plays position, size, scale and rotation steps forward on paper**, for the gizmos, the path editor's start point and Use Current's `Current`. It shares code with playback wherever it can (`UIAnimationPlayer.LayOutSteps`, a real `UIAnimationSteppedEase`, `UIAnimationPathEditor.Evaluate`) and copies the rest: From state applied first in step order; steps act in `(start time, index)` order; a step's start is read at its start time; paths at constant speed (`ConvertToConstantPathPerc`); snapping rounds. It matches the real preview within 0.07 units. It works from rest, which during a preview is what `UIAnimationPreview` captured (`NoteRest` / `ForgetRest`).

### DOTween behaviours (all silent when violated)

- `SetLink` is a no-op on a tween inside a Sequence; apply it to the outer `Sequence`.
- Configure a tween fully *before* `Append` / `Join`: `DoInsert` sets `creationLocked`, after which `From` / `SetEase` / `SetRelative` do nothing.
- `.From(value)` defaults to `setImmediately: true` and writes during construction, not at t=0.
- A stencil `Mask` defeats material instancing (`StencilMaterial.Add` caches a copy that never re-syncs). `RectMask2D` is fine; `MaterialPropertyBlock` doesn't work with UGUI at all.
- `TextMeshProUGUI` ignores `Graphic.material`: use `TMP_Text.fontMaterial`, and never destroy it.
- `SetUpdate(true)` is required for UI animating while `Time.timeScale == 0`.
- **`DOTween.Kill(x)` matches a tween's target or id, so `DOTween.Kill(sequence)` is a no-op** — not the same as `sequence.Kill()`.

**Kill semantics differ between play mode and edit mode** (DOTween 1.3.030, Unity 6000.3):

| | `tween.Kill()` | `DOTween.Kill(id)` / `KillAll()` / `Clear()` | `Pause()` | `Complete()` |
| --- | --- | --- | --- | --- |
| Play mode | immediate, `OnKill` fires | immediate, `OnKill` fires | works | `OnComplete` then `OnKill`, immediately |
| Edit mode | **nothing** — stays active, no `OnKill` | **work** | works | works, both fire |

DOTween never initialises out of play mode, and `Tween.Kill()` returns early when uninitialised; the filtered kills don't check. **A test that kills a tween in edit mode is measuring the Editor, not the game** — check tween lifetimes in play mode. Also, `DOTweenEditorPreview.Stop()` doesn't kill (it only stops calling `ManualUpdate`), so an old preview resumed on the next Play. That's why `UIAnimationPreview` tags each sequence with an id, pauses it and kills it with `DOTween.Kill(id)`.

---

## FlipbookAnimation

No dependencies, not even DOTween. It only writes `Image.sprite`, so DOTween can move, scale and fade the same object; the split is intentional.

`UISpriteFlipbook` cycles an `Image` through sprites. `UIFlipbookSelectable` sits beside a `Selectable` and picks the clip for its state (Disabled > Pressed > Selected > Highlighted > Normal, Unity's priority), leaving `onClick`, navigation and transitions alone. `UIFlipbookClipAsset` optionally shares a clip. Per clip: frames, FPS, loop mode (`Once` / `Loop` / `PingPong`), timing mode (`Constant FPS` / `Random Offset` / `Per Frame Durations`).

Intentional:

- **`stoppedExplicitly` tells a `Stop()`ped clip from one that ran out**, so `OnEnable` restarts a finished `Once` clip rather than a re-shown panel sitting frozen. `Stop()` sets it, `Play()` clears it.
- **Randomised timing and `RandomStartFrame` live on the player, never the clip** — one clip asset can drive hundreds of objects.
- **Ping-pong turns at `count - 2`**, so end frames aren't held twice: `ABCDCBABCDCB`.
- **`Selected` doesn't fall back to `Highlighted`**: a clicked button keeps selection, and borrowing the hover look reads as stuck. Empty `Selected` means no focus look.
- **Don't set the Selectable's Transition to `Sprite Swap`** — it fights the flipbook. `OnValidate` warns.
- **`LoopMode` replaced a serialized `bool Loop`**; `MigrateLoop()` reads it once, using `defaultsFilled` to tell authored data from a new element. Keep both hidden fields.
- Thumbnails use `GUI.DrawTextureWithTexCoords` over `sprite.textureRect`; `AssetPreview` loads async and returns null at first.
- The frame sort is numeric-aware: `frame_2` before `frame_10`, `frame_007` beside `frame_7`.

---

## SelectableEvents

The author's own code: UnityEvents for hover, unhover, press, release, click, select, deselect and submit on anything deriving from `Selectable`. It implements the same handler interfaces as `UIFlipbookSelectable`, which is fine — the EventSystem delivers to every handler. `OnHover` / `OnUnhover` fire regardless of `IsInteractable()`; press, click and submit don't.

---

## Shared conventions in the Claude-authored tools

- **Inspector data classes need a `FillUnsetDefaults()` called from the owner's `OnValidate`.** Unity zero-fills elements added with `+` on a `List<T>`, so initialisers don't apply. Only write fields still at zero; where false is ambiguous, a hidden `defaultsFilled` bool tells "never set" from "unticked".
- **Serialized enums are stored as integers, and the integers are the contract.** Assign every value explicitly. Changing or reusing a number silently repoints every asset: alphabetising `UIAnimationStepType` once turned 16 `Scale` steps into `GraphicAlpha` and faded a menu away. **If animations "disappear", check the enum's history first.** Fix by remapping stored integers, never by reordering back.
- **Edit-mode preview animates real scene objects, and three things are load-bearing:** capture and restore *per property* (never a blanket `EditorJsonUtility` round-trip, which blanks references like an `Image`'s sprite); re-capture baselines before every preview; clear callbacks, since On Complete runs game code. Plus: one preview at a time, one `Undo` entry, `SetDirty` on each touched object, an unwind on selection change, play-mode change and assembly reload registered from an `[InitializeOnLoad]` static constructor, and a stopped preview that is actually dead (see the DOTween table).
- **Right-click menus are scoped by target object, not type name.** `SerializedProperty.type` / `propertyPath` are unqualified, so require your own component as `serializedObject.targetObject`. `.Copy()` the property for the callback. Register `contextualPropertyMenu` once per folder. **Start a handler's entries with `Separate(menu)`, never a bare `AddSeparator`**: Unity 6000.3 already ends its part with a divider, which drew two on Windows. `Separate` reads the private `m_MenuItems`. To see a real menu, call the internal `EditorGUI.FillPropertyContextMenu(property, null, null)` by reflection from a utility window's `OnGUI` (it throws outside one) and read `m_MenuItems`.
- **Copy Inspector data with `JsonUtility`, not `EditorJsonUtility`.** The plain one keeps object references (`{"instanceID":-228892}`); the Editor one zeroes them.
- **`[Range]` clamps typed input too.** To let users type past the slider, draw the slider and field separately and only adopt the slider when it moved.
- **`GetPropertyHeight` isn't given the draw width.** For wrapping, the step drawer's `OnGUI` records `currentViewWidth - position.width` per property path (skipping `Layout` events) and the measure lays out at `currentViewWidth` minus that, calling `HandleUtility.Repaint()` when the margin changes. Anything that doesn't wrap uses fixed rows and truncates.
- **Testing enable-time logic needs reflection.** Edit mode doesn't deliver `Awake` / `OnEnable` / `OnDisable` without `[ExecuteAlways]`, so toggling `SetActive` tests nothing — invoke them directly. In play mode `AddComponent` runs `Awake` at once, so reset the lazy-init flag after filling fields.
- A namespaced tool in an un-namespaced project needs a `using` in game code that references it. Serialization only cares about the `.meta` GUID.

---

## Verifying a change

**This repo is loose folders, not a Unity project.** Nothing here can be claimed to compile without compiling it somewhere.

**`GameCraftSep2026Jam` links this repo directly**: its `Packages/manifest.json` has `"com.rmfandyplayz.dotween-ui-animation": "file:../../UnityAnimationTools/DOTweenAnimationPlayer"` (on the jam's `package-migration` branch, not yet merged). Every edit compiles there on the next `AssetDatabase.Refresh()`. **Unity writes `.meta` files into this repo** through the link, so check `git status` and commit them. `GameCraftSep2026P2Jam` installs by git URL (`#release`) and only sees what's published.

For the other two tools: copy the folder **without its `.meta` files** into a real project with DOTween Pro under `Assets/_CompileCheck/`, compile, delete, recompile. The jam's older un-namespaced copies don't conflict. Ask before using another project.

### The Unity CLI

- Open the jam with `unity open <path> --editor-version 6000.3.9f1`. It blocks: run it in the background and wait for `unity status --format json` to show `ready`.
- **The jam is `GameCraftSep2026Jam`, not `GameCraftSep2026P2Jam`, and only one should be open at a time.** Both editors take port 7800, and `unity cmd` talks to whichever holds it (`--project-path` returned 401). Check `eval 'return Application.dataPath;'` before anything that writes. `unity command` rejects `--caller` / `--skill`.
- `unity cmd recompile` usually reports a network error because the domain reload drops the connection — expected. Poll `unity cmd recompile_status`, which is authoritative; the console buffer replays stale errors.
- `unity cmd eval` runs C# in the live Editor. It mangles long scripts and **can't take `using` directives**, so call DOTween extensions statically (`DG.Tweening.TweenExtensions.Goto(t, 0.5f)`) or put the code in a compiled harness in a temp folder like `Assets/_PathCheck/Editor/`, deleted with its `.meta` and recompiled away afterwards.
- `unity cmd editor_play` / `editor_stop` switch modes. **Anything about tween lifetimes must be checked in play mode.**

### Regression fingerprints

**The play-path fingerprint is the strongest test, and needs no play mode.** In an additive scratch scene (`NewScene(..., Additive)`), instantiate `Assets/Prefabs/UI/UI.prefab`; copy the tester scenes to `Assets/_Check/` with `AssetDatabase.CopyAsset` and open those additively, so the open scene is never touched. For every player and animation, at six sample points: `EditorPrepareForPreview()`, `Random.InitState`, `player.Play(name)`, `DOTweenEditorPreview.PrepareTweenForPreview(seq, true, true, false)`, one `DOTween.ManualUpdate(t, t)`, dump, then `DOTween.Kill(id)` / `StopAll()` / `EditorRestoreBaselines()`. The dump: `EditorJsonUtility.ToJson` of every component plus `activeSelf`, and material floats/colours for anything with `UIMaterialInstance`, writing only what differs from rest. Currently 71 animations, 426 samples. **Build a fresh sequence per sample** (rewinding one accumulates on relative and `To: Current` steps), **run it twice to prove it's deterministic**, and enumerate names through `EditorCollectAnimationNames`, not `EditorAnimations`. TextMeshPro's JSON never returns to rest after an animation — stable "not restored" lines, not a bug.

Before and after are two states of this repo: `git stash push -u -- DOTweenAnimationPlayer` (**the `-u` matters**: an untracked file using new APIs breaks the old code's compile), refresh the jam, fingerprint, `git stash pop`, refresh, fingerprint, diff. Keep harnesses compatible with both versions (reflection only).

- **For target resolution or baseline changes**, a cheaper edit-mode dump is enough: `EditorPrepareForPreview()` on every player, then each runtime step's resolved-target fields, `targetPathMissed`, baseline fields and any warnings, by reflection.
- **Through the preview**: `UIAnimationPreview.End()`, `Random.InitState`, `UIAnimationPreview.Play`, read the private static `running` by reflection, `DOTween.ManualUpdate` in five slices. It differs from play mode for `ApplyFromValuesImmediately` off, and doesn't cover play-mode lifetimes.
- **Preview behaviour can be driven synchronously**: preview sequences are `UpdateType.Manual`, so `UIAnimationPreview.Play` then `DOTween.ManualUpdate(dt, dt)` in a loop inside one `eval` is exact, where sleeping between `eval` calls isn't. A plain `object` as the requester keeps inspectors' `OnDisable` out of it.

### Tester scenes

`GameCraftSep2026Jam`'s `Assets/Scenes/UI/UITester.unity` and `UITester2.unity` hold labelled hand-test stations under `UI/UIAnimation Tests`, one player per station, so the author tests by selecting objects. Fixtures are in `Assets/Testing/UI/`.

- `UITester`: S1–S4 for Shared Animations (S1 and S2 share `UITester Shared Set.asset`, each with its own On Complete on its Slide; S1 overrides its Fade; S2's Box lacks an Image so the Fade warns there only; S3 an orphaned On Complete; S4 Save as Animation Set from an `Animations` child). P1 plays the shared animations and counts each player's On Complete, P2 punch and shake spam, W1 the warning format, E1 the Ease menu. Fixtures `SharedSetTest`, `PunchSpamTest`, `WarningsTest`; the old Custom Property fixtures (`CustomPropertyTestGauge` / `Fields` / `Thrower`, `CP Counter.prefab`, `UITester Shared.asset`) are unused.
- `UITester2`: gizmos, the type check, Use Current, the CloseCredits replica, and a shared asset. Its bottom row has two **play-mode** stations with on-screen buttons: `N1` for Interrupt Others and `Stop` / `Stop(complete)` / `StopAll(complete)`, `N2` for `SetTo` / `SetFrom` / `ClearOverrides` (fixtures `InterruptOthersTest`, `SetToFromTest`), each showing what happened in white and what should have in yellow. The Canvas Scaler is Expand so that row fits a 16:9 Game view. They were checked by `EditorSceneManager.LoadSceneInPlayMode(path, Additive)` and a coroutine pressing each button with `onClick.Invoke()`.
- **A station name must not contain `/`** — `Transform.Find` reads it as a path.
- **Harnesses open scenes additively** (`OpenScene(path, Additive)`, build, `SaveScene`, `CloseScene`); `Single` discards the open scene's unsaved changes without asking. **Check the scene isn't already open first**: an additive open then returns the open scene, and `CloseScene(scene, true)` closes it, unsaved changes and all. With it open, entering play mode and `LoadSceneInPlayMode` gives two copies.
- A re-saved tester scene makes a huge, alarming diff (Unity reorders the YAML, and old players gain new fields at their defaults). Compare per YAML document (`--- !u!<class> &<fileID>`) against `HEAD`: expect no document removed and only added lines in changed ones.

### Harness traps

- **Never call `AssetDatabase.SaveAssets()`**: it flushes every dirty asset, including the jam's TMP dynamic font `Assets/Fonts/TMPro/GeistPixel Regular.asset`, whose committed copy is empty. Save your own with `AssetDatabase.SaveAssetIfDirty(asset)`. If it happens, `git checkout` the font and reimport; the open `UI.unity` may then show layout-only unsaved changes, which are harmless — tell the user rather than saving.
- **After a builder that creates assets, check `git status`** for assets it didn't mean to touch (one once re-saved UITester2's `UI Animation Set.asset`, cause unknown); revert with `git checkout`.
- **`Selection.selectionChanged` doesn't fire with the Editor in the background.** Invoke the delegate yourself (`typeof(Selection).GetField("selectionChanged", …)` → `DynamicInvoke`), then `ActiveEditorTracker.sharedTracker.ForceRebuild()`.
- **To check a drawer for exceptions without a picture**, open a utility `EditorWindow` whose `OnGUI` calls `Editor.CreateEditor(player).OnInspectorGUI()` in a try/catch, set `isExpanded` on everything, and call the window's non-public `RepaintImmediately()` by reflection.
- The Bash tool turns `\\$2` into `$2` and a `\\n` in a heredoc into a line break. Build Windows paths with `cygpath -w`, and write scripts containing backslashes with the Write tool.

### Pictures of editor UI

- `capture_scene_view` renders only the camera (no handles, gizmos or overlays) and saves under `Assets/`.
- What works is Win32 `PrintWindow(hwnd, hdc, 2)` from PowerShell, which captures the DX12 window without bringing it forward. Call `SetProcessDPIAware()` first (125% scaling clips it otherwise) and find the window with `FindWindow([NullString]::Value, title)` — PowerShell turns `$null` into `""`. For a drawer, draw it in a temporary utility `EditorWindow` with `EditorGUILayout.PropertyField` and capture that window by title.
- Scene-view gizmos: a floating `SceneView` (`CreateInstance<SceneView>()`, a unique `titleContent`, `ShowUtility()`, `Frame(bounds)`) captured by title. Every Screen Space Overlay canvas sits at the same world position, so `SceneVisibilityManager.instance.Isolate(go, true)`, then `ExitIsolation()`.
- Scene-view interaction via `SceneView.SendEvent`: window coordinates arrive shifted (about 50 points in y), so calibrate with one `MouseMove` and a `duringSceneGui` probe first.

### Compile checks without the Editor

**On the author's Windows machine, Editor closed (about a minute).** Unity's compiler response files are in `GameCraftSep2026Jam/Library/Bee/artifacts/2000b0aE.dag/` (`2000b0aP.dag/` is the player build, without `UNITY_EDITOR`); the animator's are `rmf_claude.DOTweenUI.rsp` and `rmf_claude.DOTweenUI.Editor.rsp`, whose source lines are already this repo's files. Copy them to the scratchpad, replace `-out:` / `-refout:` with an `-out:` there, and point the editor one's runtime reference at the DLL just built. Run from the jam root: `"<Unity>/Editor/Data/NetCoreRuntime/dotnet.exe" exec <csc.dll> -nologo @<rsp>`, with `csc.dll` from `Editor/Data/DotNetSdkRoslyn/`. In Git Bash, prefix `MSYS2_ARG_CONV_EXCL='*'` and pass `csc.dll` and the `@rsp` as `cygpath -w` paths. This recipe was worked out on the pre-package `Assembly-CSharp` rsps and hasn't been rerun since. It proves compilation against real 6000.3, DOTween, TMP and UGUI, not behaviour.

**Offline, in a cloud container, no Unity.** Proves compilation against real assemblies and that pure logic (reflection, baselines, DOTween tweens) behaves. It can't cover the native engine: `GetComponent`, `transform.Find`, IMGUI, Undo, the preview's editor hooks.

- **Toolchain**: `apt-get install dotnet-sdk-8.0 mono-devel`. The Ubuntu archive is reachable; the `dot.net` installer, NuGet's search host and `codeload.github.com` are blocked.
- **Unity references** from `https://api.nuget.org/v3-flatcontainer/<id>/<version>/<id>.<version>.nupkg`: `unity3d.sdk` 2021.1.14.1 (`UnityEditor.dll` and a real monolithic `UnityEngine.dll`), `unityengine.modules` 2021.3.33 (reference-only module DLLs, every body `throw null`), `unity3d.unityengine.ui` 2020.3.21. Compile against the modules, not the monolithic DLL, so UnityEngine.UI's types unify.
- **DOTween**: `git clone --depth 1 -b develop https://github.com/Demigiant/dotween`; `UnityTests.Unity6000.3/Assets/Plugins/Demigiant/DOTween/` has `DOTween.dll`, `Editor/DOTweenEditor.dll` and the generated `Modules/*.cs` (1.3.045 then, against the jam's 1.3.030).
- **Three `net48` projects**, split as Unity does, with `Microsoft.NETFramework.ReferenceAssemblies.net48`: firstpass (`Modules/*.cs`); runtime (the tool's `.cs` outside `Editor/`, plus a TMP stub `TMP_Text : MaskableGraphic` with `fontSharedMaterial` / `fontMaterial`); editor (`Editor/**/*.cs` + `UnityEditor` + `DOTweenEditor`). Define `UNITY_EDITOR;UNITY_2018_1_OR_NEWER;…;UNITY_2021_3_OR_NEWER;NET_4_6`, and build the runtime again without `UNITY_EDITOR`.
- **One known false error**: `SerializedProperty.boxedValue` (2022.1+) isn't in the 2021.1 `UnityEditor.dll`. Shim it in a scratch copy, not the real files.
- **Running tests needs Mono**: build one facade assembly per module name that `[TypeForwardedTo]`s every public type into the monolithic `UnityEngine.dll` (dropping `[Obsolete(error: true)]` types); patch that DLL with Mono.Cecil so every `InternalCall` returns `default` and `UnityEngine.Object`'s static constructor is a bare `ret`; put everything in one folder and run `mono tests.exe`.
- **Faking a live component**: `FormatterServices.GetUninitializedObject`, then set `UnityEngine.Object.m_CachedPtr` non-zero (look it up with `Public | NonPublic`). Inject resolved members into a step by reflection, since `Resolve` needs `GetComponents`.
- **DOTween runs uninitialised here**, as in edit mode. `SetUpdate(UpdateType.Manual)` plus `DOTween.ManualUpdate(dt, dt)` steps a tween or sequence exactly.
