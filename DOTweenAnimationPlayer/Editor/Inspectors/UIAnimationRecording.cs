// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, October 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>A value read from the scene for a From or To row, in the row's own terms, ready to store.</summary>
    internal struct UIAnimationReading
    {
        public UIAnimationValueKind Kind;
        public bool TwoDimensional;

        /// <summary>A number in x, a vector in xyz, a colour as rgba.</summary>
        public Vector4 Number;

        public string Text;
    }

    /// <summary>
    /// Recording: the record button at the end of every From and To row in a pop-up animation window
    /// (UIAnimationWindow), which copies a value out of the scene into the row, and the session it starts.
    ///
    /// The first click starts a session. It holds a preview open on the player (UIAnimationPreview.Hold)
    /// - a new one starts where things stand, so nothing moves - and that preview captures every resting
    /// value before anything is posed: Baseline measures from those, and they are what goes back at the
    /// end. While it runs, the row shows what it will become, live, and more rows can be added. The user
    /// poses the objects any way they like - the Move tool, the Inspector - and Finish writes every row
    /// and puts the objects back; Cancel only puts them back. Closing the window is Finish.
    ///
    /// A row stores whatever its mode needs for the step to land exactly where the object was posed: the
    /// value itself for Absolute, its difference from the resting value for Baseline, its difference from
    /// where the step starts for Current (UIAnimationSimulation works that out). The mode is never changed.
    ///
    /// A Custom Property's getter is arbitrary code, and everything on screen redraws constantly, so its
    /// value is only read on Finish - which is a click, like the old button was.
    ///
    /// Finish and Cancel fold everything since the preview captured the resting state - the posing, and
    /// what Finish wrote - into one Undo step. Undoing a single pose after the objects were put back would
    /// otherwise move that object back to where it was posed from, out of the preview, for good.
    ///
    /// Rows are read in the order the steps run, and a Current row is measured from where its step starts
    /// once the rows before it have been written (ReadAll), so every row lands where the object was posed
    /// even when an earlier row moves where it starts. Two rows reading the same value get the same pose,
    /// which the panel warns about rather than refuses: a step starting where another ends wants exactly that.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIAnimationRecording
    {
        /// <summary>A row being recorded: which step of the window's animation, which end, and what the step was.</summary>
        private struct Row
        {
            public int Step;
            public bool From;
            public string Fingerprint;

            // A material row's asset and its value when the row was added, to catch the asset being edited
            // instead of the copy the object shows while recording.
            public Material Source;
            public Vector4 SourceValue;
        }

        /// <summary>What one row would store, as ReadAll works it out.</summary>
        private struct Result
        {
            public bool Ok;
            public bool Deferred;
            public UIAnimationReading Reading;
            public string Reason;
        }

        private static UIAnimationWindow window;
        private static UIAnimationPlayer player;
        private static readonly List<Row> rows = new List<Row>();

        // ReadAll's results for the rows, from the panel at the top of the window's last OnGUI, so each row's
        // field further down shows the same thing without working it all out again.
        private static readonly List<Result> shown = new List<Result>();

        // Set while Finish and Cancel end the preview themselves, so that is not taken for the preview
        // being ended from outside.
        private static bool closing;

        static UIAnimationRecording()
        {
            UIAnimationPreview.Ended += OnPreviewEnded;
        }

        /// <summary>The window recording right now, or null.</summary>
        public static UIAnimationWindow Window
        {
            get { return window; }
        }

        public static bool IsRecording(UIAnimationWindow candidate)
        {
            return window != null && window == candidate;
        }

        /// <summary>Whether this row is being recorded.</summary>
        public static bool IsArmed(SerializedProperty step, bool from)
        {
            if (window == null || UIAnimationWindow.Of(step.serializedObject) != window) return false;

            UIAnimation animation;
            UIAnimationStep data;
            int index;

            return window.TryGetStep(step, out animation, out data, out index) && IndexOf(index, from) >= 0;
        }

        /// <summary>Whether this row's record button can be pressed, and if not, why. Reads nothing.</summary>
        public static bool CanRecord(SerializedProperty step, bool from, out string reason)
        {
            UIAnimationWindow owner = UIAnimationWindow.Of(step.serializedObject);
            UIAnimation animation;
            UIAnimationStep data;
            int index;

            if (owner == null || !owner.TryGetStep(step, out animation, out data, out index))
            {
                reason = "This step is not part of the window's animation.";
                return false;
            }

            if (window != null && window != owner)
            {
                reason = "'" + window.titleContent.text + "' is recording in another window. Finish or Cancel it there first.";
                return false;
            }

            return CanRead(data, from, owner.Owner, out reason);
        }

        /// <summary>
        /// The record button: starts a session on its first press, adds this row to the one running, or takes
        /// it back out again.
        /// </summary>
        public static void Toggle(SerializedProperty step, bool from)
        {
            UIAnimationWindow owner = UIAnimationWindow.Of(step.serializedObject);
            UIAnimation animation;
            UIAnimationStep data;
            int index;

            if (owner == null || !owner.TryGetStep(step, out animation, out data, out index)) return;

            if (window == owner)
            {
                int armed = IndexOf(index, from);
                if (armed >= 0)
                {
                    Drop(armed);
                    return;
                }
            }
            else if (window != null)
            {
                return;
            }

            UIAnimationPlayer scene = owner.Owner;

            string reason;
            if (!CanRead(data, from, scene, out reason)) return;

            if (window == null)
            {
                // A preview already running is joined where it stands, and refreshed so it covers this step.
                if (!UIAnimationPreview.Hold(owner, scene)) return;

                window = owner;
                player = scene;
                rows.Clear();
            }
            else
            {
                // A step or target added since the preview began was never captured at rest, so it would
                // have nothing to measure Baseline from and would not be put back. What a second Play
                // does first fixes both.
                UIAnimationPreview.Refresh(scene);
            }

            var row = new Row { Step = index, From = from, Fingerprint = FingerprintOf(data) };
            NoteSource(ref row, data, scene);
            rows.Add(row);

            // Listed in the order the steps run, From before To, however they were clicked.
            rows.Sort((a, b) => a.Step != b.Step ? a.Step.CompareTo(b.Step) : b.From.CompareTo(a.From));

            owner.Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>Writes every row, then ends the preview, putting every object back. Closing the window does this.</summary>
        public static void Finish()
        {
            if (window == null) return;

            UIAnimationWindow owner = window;
            UIAnimationPlayer scene = player;
            var failures = new List<string>();

            // The objects go back whatever happens while reading - a getter can throw anything.
            try
            {
                SerializedObject serialized = owner.Serialized;
                UIAnimation animation = owner.Animation;

                if (serialized != null && serialized.targetObject != null && animation != null && scene != null)
                {
                    serialized.Update();

                    var results = new List<Result>();
                    ReadAll(animation, scene, true, results);

                    for (int i = 0; i < rows.Count; i++)
                    {
                        Row row = rows[i];

                        if (!results[i].Ok)
                        {
                            failures.Add(Describe(owner, row) + ": " + results[i].Reason);
                            continue;
                        }

                        SerializedProperty step = serialized.FindProperty(owner.StepPath(row.Step));
                        if (step != null) Write(step, row.From, results[i].Reading);
                    }

                    serialized.ApplyModifiedProperties();
                }
            }
            finally
            {
                Close(owner, "Record Animation Values");
            }

            for (int i = 0; i < failures.Count; i++)
            {
                UIAnimationLog.Warn("Recording", "A VALUE WAS NOT RECORDED.", failures[i], scene);
            }
        }

        /// <summary>Ends the preview, putting every object back, and writes nothing.</summary>
        public static void Cancel()
        {
            if (window == null) return;

            Close(window, "Cancel Recording");
        }

        private static void Close(UIAnimationWindow owner, string undoName)
        {
            int group = UIAnimationPreview.UndoGroup;

            closing = true;
            window = null;
            player = null;
            rows.Clear();
            shown.Clear();

            try
            {
                UIAnimationPreview.Release(owner);
                UIAnimationPreview.End();
            }
            finally
            {
                closing = false;
            }

            if (group >= 0) Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(undoName);

            if (owner != null) owner.Repaint();
            InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// The preview was ended from outside - play mode, a recompile, another player's preview starting.
        /// Everything is already back at rest, and with the preview gone there is nothing left to measure
        /// from, so the session is over and the pose is lost.
        /// </summary>
        private static void OnPreviewEnded()
        {
            if (closing || window == null) return;

            UIAnimationWindow owner = window;
            UIAnimationPlayer scene = player;
            int count = rows.Count;

            window = null;
            player = null;
            rows.Clear();
            shown.Clear();

            if (count > 0)
            {
                UIAnimationLog.Warn("Recording", "IT WAS CANCELLED - NOTHING WAS WRITTEN.",
                    "The preview it was recording from ended under it: play mode, a recompile, its scene closing, or a " +
                    "preview started on another player. Every object is back where it was.", scene);
            }

            if (owner != null) owner.Repaint();
        }

        /// <summary>Stops recording one row. The last one going is Cancel: a session recording nothing has nothing to do.</summary>
        private static void Drop(int index)
        {
            UIAnimationWindow owner = window;

            rows.RemoveAt(index);
            if (index < shown.Count) shown.RemoveAt(index);

            if (rows.Count == 0) Cancel();
            else if (owner != null) owner.Repaint();
        }

        private static int IndexOf(int step, bool from)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Step == step && rows[i].From == from) return i;
            }

            return -1;
        }

        /// <summary>
        /// Whether a row still means what it meant when it was added. Rows are kept by step index, so a step
        /// moved, retyped or pointed elsewhere mid-session would otherwise be written with another step's value.
        /// </summary>
        private static bool StillThere(UIAnimation animation, Row row)
        {
            if (row.Step >= animation.Steps.Count) return false;

            UIAnimationStep step = animation.Steps[row.Step];
            if (step == null || FingerprintOf(step) != row.Fingerprint) return false;

            return !row.From || step.UseFrom;
        }

        private static string FingerprintOf(UIAnimationStep step)
        {
            Object slot = UIAnimationTargets.SlotOf(step, step.Type);

            return (int)step.Type + "|" + (slot != null ? slot.GetInstanceID() : 0) + "|" + step.TargetPath + "|" +
                   (step.Type == UIAnimationStepType.CustomProperty
                       ? step.PropertyComponent + "." + step.PropertyMember + ":" + (int)step.PropertyKind
                       : step.ShaderProperty);
        }

        private static string Describe(UIAnimationWindow owner, Row row)
        {
            return "'" + owner.AnimationName + "' step " + row.Step + " " + (row.From ? "From" : "To");
        }

        // ---------------------------------------------------------------- reading

        /// <summary>
        /// Whether a row could be recorded from this player's scene, judged without reading anything: a target
        /// to read, a Custom Property member that is there, a material that has the property.
        /// </summary>
        private static bool CanRead(UIAnimationStep step, bool from, UIAnimationPlayer owner, out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "Not in play mode - values changed there are thrown away when it ends.";
                return false;
            }

            if (owner == null)
            {
                reason = "A shared animation set has no scene to record from. Fill Preview On at the top of this " +
                         "window with a player that uses the set.";
                return false;
            }

            if (EditorUtility.IsPersistent(owner))
            {
                reason = "'" + owner.name + "' is in a prefab asset, not a scene. Open the prefab, or record from an " +
                         "instance of it in a scene.";
                return false;
            }

            if (from && step.FromMode == UIAnimationEndpointMode.Current)
            {
                reason = "This From is set to Current, which ignores the value here.";
                return false;
            }

            string problem;
            Object target = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath,
                owner.gameObject, out problem);

            if (target == null)
            {
                reason = problem ?? "This step has nothing to record a value from.";
                return false;
            }

            if (step.Type == UIAnimationStepType.CustomProperty)
            {
                Component component;
                MemberInfo member;

                if (!UIAnimationProperties.TryLocate(target as GameObject, step.PropertyComponent, step.PropertyMember,
                        step.PropertyKind, out component, out member, out reason))
                {
                    return false;
                }
            }
            else if (UIAnimationStep.TargetKindOf(step.Type) == UIAnimationTargetKind.Material)
            {
                Material material = UIAnimationTargets.SourceMaterialOf(target as UIMaterialInstance);

                if (material == null)
                {
                    reason = "'" + target.name + "' has no material to record from.";
                    return false;
                }

                if (string.IsNullOrEmpty(step.ShaderProperty) || !material.HasProperty(step.ShaderProperty))
                {
                    reason = "Shader '" + material.shader.name + "' has no property '" + step.ShaderProperty + "'.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// What recording this row would store, from what the scene holds now. allowGetters is false on every
        /// repaint: a Custom Property is then left unread, and deferred says it is read on Finish.
        /// </summary>
        private static bool TryRead(UIAnimation animation, int stepIndex, bool from, UIAnimationPlayer owner, bool allowGetters,
            out UIAnimationReading reading, out bool deferred, out string reason)
        {
            reading = default(UIAnimationReading);
            deferred = false;

            UIAnimationStep step = animation.Steps[stepIndex];
            if (!CanRead(step, from, owner, out reason)) return false;

            bool custom = step.Type == UIAnimationStepType.CustomProperty;
            reading.Kind = custom ? UIAnimationStep.ValueKindOf(step.PropertyKind) : UIAnimationStep.ValueKindOf(step.Type);
            reading.TwoDimensional = custom
                ? step.PropertyKind == UIAnimationPropertyKind.Vector2
                : UIAnimationStep.IsTwoDimensional(step.Type);

            string problem;
            Object target = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath,
                owner.gameObject, out problem);

            Vector4 live;
            string liveText = null;

            if (custom)
            {
                if (!allowGetters)
                {
                    deferred = true;
                    return true;
                }

                if (!ReadMember(step, target as GameObject, out live, out liveText, out reason)) return false;
            }
            else if (!ReadLive(step, target, out live))
            {
                reason = "This step has nothing to record a value from.";
                return false;
            }

            UIAnimationEndpointMode mode = from ? step.FromMode : step.ToMode;

            // A Current To is only an offset from where the step starts when there is no From. With one, the
            // row offers Absolute and Baseline only, and shows - and stores - a Current as Baseline.
            if (!from && mode == UIAnimationEndpointMode.Current && step.UseFrom) mode = UIAnimationEndpointMode.Baseline;

            bool rotation = step.Type == UIAnimationStepType.Rotation;
            GameObject scene = owner.gameObject;

            switch (mode)
            {
                case UIAnimationEndpointMode.Absolute:
                    reading.Number = rotation ? WrapAngles(live) : live;
                    reading.Text = liveText;
                    break;

                case UIAnimationEndpointMode.Baseline:
                {
                    Vector4 rest;
                    string restText;

                    if (!UIAnimationSimulation.TryRestOf(step, scene, out rest, out restText))
                    {
                        reason = "Its resting value is not known, so there is nothing to measure Baseline from.";
                        return false;
                    }

                    if (reading.Kind == UIAnimationValueKind.Text)
                    {
                        // Baseline text is the resting text with this added onto the end.
                        string head = restText ?? string.Empty;
                        string now = liveText ?? string.Empty;

                        if (!now.StartsWith(head, System.StringComparison.Ordinal))
                        {
                            reason = "Baseline adds text onto the end of the resting text (\"" + Shorten(head) + "\"), and the " +
                                     "text no longer starts with it. Switch to Absolute to record the text as it is.";
                            return false;
                        }

                        reading.Text = now.Substring(head.Length);
                    }
                    else
                    {
                        reading.Number = live - rest;
                        if (rotation) reading.Number = WrapAngles(reading.Number);
                    }

                    break;
                }

                default:
                {
                    Vector4 start;

                    if (reading.Kind == UIAnimationValueKind.Text
                        || !UIAnimationSimulation.TryStartOf(animation, scene, stepIndex, out start))
                    {
                        reason = "Where this step starts cannot be worked out, so there is nothing to measure Current from.";
                        return false;
                    }

                    reading.Number = live - start;
                    if (rotation) reading.Number = WrapAngles(reading.Number);
                    break;
                }
            }

            if (reading.Kind == UIAnimationValueKind.Int) reading.Number.x = Mathf.Round(reading.Number.x);

            // The same rule as dragging an endpoint in the Scene view: a snapping step stores whole numbers,
            // halves rounded away from zero. Z is left alone, as it is never snapped.
            if (reading.Kind == UIAnimationValueKind.Vector && UIAnimationStep.SupportsSnapping(step.Type)
                && (step.Snapping || animation.SnapsEveryStep))
            {
                reading.Number.x = (float)System.Math.Round(reading.Number.x, System.MidpointRounding.AwayFromZero);
                reading.Number.y = (float)System.Math.Round(reading.Number.y, System.MidpointRounding.AwayFromZero);
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// What every row would store, in order. A Current row measures from where its step starts, and the rows
        /// before it may move that - an earlier step on the same object, recorded in the same go - so it is
        /// measured on a copy of the animation with those rows already written in: what the animation will be
        /// once Finish has written them. Without that, the later row is measured from where its step used to
        /// start, and plays on from the new pose by the old difference. A Custom Property is left unread unless
        /// allowGetters; no row after it reads the same member any differently.
        /// </summary>
        private static void ReadAll(UIAnimation animation, UIAnimationPlayer owner, bool allowGetters, List<Result> into)
        {
            into.Clear();

            int lastFromStart = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (MeasuresFromStart(animation, rows[i])) lastFromStart = i;
            }

            UIAnimation working = null;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                var result = new Result();

                if (!StillThere(animation, row))
                {
                    result.Reason = "it changed while it was being recorded - moved, retyped or pointed at something " +
                                    "else - so nothing was written to it.";
                    into.Add(result);
                    continue;
                }

                UIAnimation source = working != null && MeasuresFromStart(animation, row) ? working : animation;

                result.Ok = TryRead(source, row.Step, row.From, owner, allowGetters, out result.Reading, out result.Deferred,
                    out result.Reason);

                if (result.Ok && !result.Deferred && i < lastFromStart)
                {
                    if (working == null) working = JsonUtility.FromJson<UIAnimation>(JsonUtility.ToJson(animation));
                    Apply(working.Steps[row.Step], row.From, result.Reading);
                }

                into.Add(result);
            }
        }

        /// <summary>Whether a row's value depends on where its step starts: a To on Current, with no From.</summary>
        private static bool MeasuresFromStart(UIAnimation animation, Row row)
        {
            if (row.From || row.Step >= animation.Steps.Count || animation.Steps[row.Step] == null) return false;

            UIAnimationStep step = animation.Steps[row.Step];
            return step.ToMode == UIAnimationEndpointMode.Current && !step.UseFrom;
        }

        /// <summary>Write, into a step itself rather than through a SerializedObject - the working copy in ReadAll.</summary>
        private static void Apply(UIAnimationStep step, bool from, UIAnimationReading reading)
        {
            switch (reading.Kind)
            {
                case UIAnimationValueKind.Vector:
                {
                    Vector3 current = from ? step.FromVector : step.ToVector;
                    Vector3 value = reading.TwoDimensional
                        ? new Vector3(reading.Number.x, reading.Number.y, current.z)
                        : (Vector3)reading.Number;

                    if (from) step.FromVector = value;
                    else step.ToVector = value;
                    break;
                }

                case UIAnimationValueKind.Color:
                    if (from) step.FromColor = reading.Number;
                    else step.ToColor = reading.Number;
                    break;

                case UIAnimationValueKind.Text:
                    if (from) step.FromText = reading.Text ?? string.Empty;
                    else step.ToText = reading.Text ?? string.Empty;
                    break;

                default:
                    if (from) step.FromFloat = reading.Number.x;
                    else step.ToFloat = reading.Number.x;
                    break;
            }
        }

        /// <summary>
        /// What a row reads, as a key two rows reading the same value share, and how to name that value to the
        /// user. Position covers both position types, which move one another; a Graphic's alpha is part of its
        /// colour. False when there is nothing to read.
        /// </summary>
        private static bool TryValueOf(UIAnimationStep step, UIAnimationPlayer owner, out string key, out string label)
        {
            key = null;
            label = null;
            if (step == null || owner == null) return false;

            if (UIAnimationRectState.Covers(step.Type))
            {
                RectTransform rect = UIAnimationTargets.RectOf(step, owner.gameObject);
                if (rect == null) return false;

                string what;
                switch (step.Type)
                {
                    case UIAnimationStepType.AnchoredPosition:
                    case UIAnimationStepType.LocalPosition: what = "position"; break;
                    case UIAnimationStepType.SizeDelta: what = "size"; break;
                    case UIAnimationStepType.Scale: what = "scale"; break;
                    case UIAnimationStepType.Rotation: what = "rotation"; break;
                    case UIAnimationStepType.OffsetMin: what = "Offset Min"; break;
                    default: what = "Offset Max"; break;
                }

                key = rect.GetInstanceID() + "|" + what;
                label = rect.name + "'s " + what;
                return true;
            }

            UIAnimationChannel channel;
            if (!UIAnimationSimulation.TryChannelOf(step, owner.gameObject, out channel) || channel.Target == null) return false;

            string member;
            switch (UIAnimationStep.TargetKindOf(step.Type))
            {
                case UIAnimationTargetKind.CanvasGroup: member = "alpha"; break;
                case UIAnimationTargetKind.Graphic: member = "colour"; break;
                case UIAnimationTargetKind.Material: member = "material " + channel.Member; break;
                default: member = channel.Member; break;
            }

            key = channel.Target.GetInstanceID() + "|" + channel.Member;
            label = channel.Target.name + "'s " + member;
            return true;
        }

        /// <summary>
        /// Where a row's value can be changed by hand: the component that holds it, or - for a material - the copy
        /// the object shows while recording, since editing the material asset changes nothing on screen.
        /// </summary>
        private static Object EditableOf(UIAnimationStep step, UIAnimationPlayer owner)
        {
            if (step == null || owner == null) return null;

            string problem;
            Object target = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath,
                owner.gameObject, out problem);

            if (step.Type == UIAnimationStepType.CustomProperty)
            {
                Component component;
                MemberInfo member;

                return UIAnimationProperties.TryLocate(target as GameObject, step.PropertyComponent, step.PropertyMember,
                    step.PropertyKind, out component, out member, out problem)
                    ? component
                    : target;
            }

            var instance = target as UIMaterialInstance;
            if (instance != null)
            {
                Material shown = UIAnimationTargets.SourceMaterialOf(instance);
                return shown != null ? (Object)shown : instance;
            }

            return target;
        }

        /// <summary>A material row's asset and its value now, if the preview has given the object a copy of it.</summary>
        private static void NoteSource(ref Row row, UIAnimationStep step, UIAnimationPlayer owner)
        {
            row.Source = SourceOf(step, owner);
            if (row.Source != null) row.SourceValue = ValueIn(row.Source, step);
        }

        private static Material SourceOf(UIAnimationStep step, UIAnimationPlayer owner)
        {
            if (UIAnimationStep.TargetKindOf(step.Type) != UIAnimationTargetKind.Material || owner == null) return null;

            string problem;
            var instance = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath,
                owner.gameObject, out problem) as UIMaterialInstance;

            Material source = instance != null ? instance.EditorSource : null;
            return source != null && source != UIAnimationTargets.SourceMaterialOf(instance) ? source : null;
        }

        private static Vector4 ValueIn(Material material, UIAnimationStep step)
        {
            if (material == null || string.IsNullOrEmpty(step.ShaderProperty) || !material.HasProperty(step.ShaderProperty))
            {
                return Vector4.zero;
            }

            return step.Type == UIAnimationStepType.MaterialColor
                ? (Vector4)material.GetColor(step.ShaderProperty)
                : new Vector4(material.GetFloat(step.ShaderProperty), 0f, 0f, 0f);
        }

        /// <summary>The value the target holds right now, in the step's own terms. Never a Custom Property.</summary>
        private static bool ReadLive(UIAnimationStep step, Object target, out Vector4 value)
        {
            value = Vector4.zero;
            UIAnimationStepType type = step.Type;

            var rect = target as RectTransform;
            if (rect != null && UIAnimationRectState.Covers(type))
            {
                value = UIAnimationRectState.Of(rect).Read(type);
                return true;
            }

            var group = target as CanvasGroup;
            if (group != null && type == UIAnimationStepType.CanvasGroupAlpha)
            {
                value = new Vector4(group.alpha, 0f, 0f, 0f);
                return true;
            }

            var graphic = target as UnityEngine.UI.Graphic;
            if (graphic != null && type == UIAnimationStepType.GraphicColor)
            {
                value = graphic.color;
                return true;
            }

            if (graphic != null && type == UIAnimationStepType.GraphicAlpha)
            {
                value = new Vector4(graphic.color.a, 0f, 0f, 0f);
                return true;
            }

            // During the session the preview has given the Graphic its instance, so this is what was posed.
            var instance = target as UIMaterialInstance;
            if (instance != null)
            {
                Material material = UIAnimationTargets.SourceMaterialOf(instance);
                if (material == null || string.IsNullOrEmpty(step.ShaderProperty) || !material.HasProperty(step.ShaderProperty)) return false;

                value = type == UIAnimationStepType.MaterialColor
                    ? (Vector4)material.GetColor(step.ShaderProperty)
                    : new Vector4(material.GetFloat(step.ShaderProperty), 0f, 0f, 0f);
                return true;
            }

            return false;
        }

        /// <summary>
        /// A Custom Property's value, read through its getter - only ever on a click. A counted number is the
        /// number its text shows, with the text alongside.
        /// </summary>
        private static bool ReadMember(UIAnimationStep step, GameObject host, out Vector4 number, out string text, out string reason)
        {
            number = Vector4.zero;
            text = null;

            Component component;
            MemberInfo member;

            if (!UIAnimationProperties.TryLocate(host, step.PropertyComponent, step.PropertyMember, step.PropertyKind,
                    out component, out member, out reason))
            {
                return false;
            }

            object read;

            try
            {
                read = UIAnimationProperties.Read(component, member);
            }
            catch (System.Exception exception)
            {
                // Reflection wraps whatever the getter threw.
                System.Exception cause = exception.InnerException ?? exception;
                reason = "Reading " + UIAnimationProperties.Describe(component.GetType().FullName, member.Name, step.PropertyKind) +
                         " threw " + cause.GetType().Name + " (" + cause.Message + ").";
                return false;
            }

            switch (step.PropertyKind)
            {
                case UIAnimationPropertyKind.Float:
                    number.x = (float)read;
                    break;

                case UIAnimationPropertyKind.Int:
                    number.x = (int)read;
                    break;

                case UIAnimationPropertyKind.Vector2:
                {
                    var read2 = (Vector2)read;
                    number = new Vector4(read2.x, read2.y, 0f, 0f);
                    break;
                }

                case UIAnimationPropertyKind.Vector3:
                    number = (Vector3)read;
                    break;

                case UIAnimationPropertyKind.Color:
                    number = (Color)read;
                    break;

                case UIAnimationPropertyKind.Text:
                    text = (string)read ?? string.Empty;
                    break;

                default:
                    text = (string)read ?? string.Empty;
                    number.x = UIAnimationProperties.ParseNumber(text);
                    break;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// Rotation reads back as 0 to 360, so -10 would come out as 350 - and a Rotation step turns the long way
        /// round to 350. Nearest to zero is what was almost always meant.
        /// </summary>
        private static Vector4 WrapAngles(Vector4 angles)
        {
            return new Vector4(Mathf.DeltaAngle(0f, angles.x), Mathf.DeltaAngle(0f, angles.y), Mathf.DeltaAngle(0f, angles.z), 0f);
        }

        private static string Shorten(string text)
        {
            return text.Length <= 24 ? text : text.Substring(0, 24) + "…";
        }

        /// <summary>A reading into the row's own field. A Z the row never shows is left as it was rather than zeroed.</summary>
        private static void Write(SerializedProperty step, bool from, UIAnimationReading reading)
        {
            string prefix = from ? "From" : "To";

            switch (reading.Kind)
            {
                case UIAnimationValueKind.Vector:
                {
                    SerializedProperty value = step.FindPropertyRelative(prefix + "Vector");
                    Vector3 current = value.vector3Value;

                    value.vector3Value = reading.TwoDimensional
                        ? new Vector3(reading.Number.x, reading.Number.y, current.z)
                        : (Vector3)reading.Number;
                    break;
                }

                case UIAnimationValueKind.Color:
                    step.FindPropertyRelative(prefix + "Color").colorValue = reading.Number;
                    break;

                case UIAnimationValueKind.Text:
                    step.FindPropertyRelative(prefix + "Text").stringValue = reading.Text ?? string.Empty;
                    break;

                // An int member's value is kept in the float field.
                default:
                    step.FindPropertyRelative(prefix + "Float").floatValue = reading.Number.x;
                    break;
            }
        }

        // ---------------------------------------------------------------- drawing

        private static readonly Color ArmedTint = new Color(0.95f, 0.25f, 0.2f, 0.16f);

        public const string ButtonTooltip =
            "Record: copies this value from the scene.\n\n" +
            "Click it, then pose the object in the Scene view - move, resize or recolour it, or change it in its " +
            "Inspector - and press Finish at the top of this window. Nothing moves when it starts, the row shows " +
            "the value it will get while you pose, and Finish puts every object back afterwards. Click more " +
            "rows to record several at once; click a recording row again to stop recording it.";

        private const string DeferredTooltip =
            "A Custom Property is read through its getter, which is code - so it is read once, when you press " +
            "Finish, rather than every time the window redraws.";

        public static Texture RecordIcon
        {
            get { return EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_Animation.Record" : "Animation.Record"); }
        }

        private static GUIStyle wrapStyle;

        private static GUIStyle WrapStyle
        {
            get
            {
                if (wrapStyle == null) wrapStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return wrapStyle;
            }
        }

        /// <summary>A recording row's value field: what Finish will write, read-only and live.</summary>
        public static void DrawArmedValue(Rect rect, SerializedProperty step, bool from)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(rect.x - 2f, rect.y - 1f, rect.width + 4f, rect.height + 2f), ArmedTint);
            }

            UIAnimation animation;
            UIAnimationStep data;
            int index;

            if (window == null || player == null || !window.TryGetStep(step, out animation, out data, out index)) return;

            // What the panel at the top worked out this pass. Read again only if the panel has not run.
            int armed = IndexOf(index, from);
            if (armed < 0) return;

            if (shown.Count != rows.Count) ReadAll(animation, player, false, shown);

            DrawResult(rect, shown[armed], Offsets(data, from));
        }

        private static void DrawResult(Rect rect, Result result, bool offset)
        {
            if (!result.Ok)
            {
                GUI.Label(rect, new GUIContent(result.Reason, result.Reason), EditorStyles.miniLabel);
                return;
            }

            if (result.Deferred)
            {
                GUI.Label(rect, new GUIContent("read on Finish", DeferredTooltip), EditorStyles.miniLabel);
                return;
            }

            DrawReading(rect, result.Reading, offset);
        }

        /// <summary>Whether a row stores a difference rather than the value itself: Baseline, or Current.</summary>
        private static bool Offsets(UIAnimationStep step, bool from)
        {
            return (from ? step.FromMode : step.ToMode) != UIAnimationEndpointMode.Absolute;
        }

        private static void DrawReading(Rect rect, UIAnimationReading reading, bool offset)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                switch (reading.Kind)
                {
                    case UIAnimationValueKind.Vector:
                        if (reading.TwoDimensional) EditorGUI.Vector2Field(rect, GUIContent.none, reading.Number);
                        else EditorGUI.Vector3Field(rect, GUIContent.none, reading.Number);
                        break;

                    case UIAnimationValueKind.Color:
                        EditorGUI.ColorField(rect, GUIContent.none, reading.Number, false, true, false);
                        break;

                    case UIAnimationValueKind.Int:
                        EditorGUI.IntField(rect, Mathf.RoundToInt(reading.Number.x));
                        break;

                    case UIAnimationValueKind.Text:
                        EditorGUI.TextField(rect, reading.Text ?? string.Empty);
                        break;

                    default:
                        EditorGUI.FloatField(rect, reading.Number.x);
                        break;
                }
            }

            // A colour stored as a difference has negative parts a swatch cannot show, so the numbers are a hover away.
            if (reading.Kind == UIAnimationValueKind.Color)
            {
                Vector4 c = reading.Number;
                string numbers = "R " + c.x.ToString("0.###") + "   G " + c.y.ToString("0.###") + "   B " + c.z.ToString("0.###") +
                                 "   A " + c.w.ToString("0.###");

                GUI.Label(rect, new GUIContent(string.Empty, offset
                    ? "Stored as a difference from where it starts or rests, which a swatch cannot show:\n" + numbers
                    : numbers), GUIStyle.none);
            }
        }

        private static GUIContent revealContent;

        private static GUIContent RevealContent
        {
            get
            {
                if (revealContent == null)
                {
                    revealContent = new GUIContent(EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin
                        ? "d_Animation.FilterBySelection"
                        : "Animation.FilterBySelection"));
                }

                return revealContent;
            }
        }

        private static GUIStyle iconButtonStyle;

        private static GUIStyle IconButtonStyle
        {
            get
            {
                if (iconButtonStyle == null)
                {
                    iconButtonStyle = new GUIStyle(EditorStyles.miniButton) { padding = new RectOffset(2, 2, 2, 2) };
                }

                return iconButtonStyle;
            }
        }

        /// <summary>
        /// The box at the top of a pop-up animation window: what is being recorded, with Finish and Cancel, or
        /// - when nothing is - one line saying how to start.
        /// </summary>
        public static void DrawPanel(UIAnimationWindow owner)
        {
            if (window == null)
            {
                EditorGUILayout.LabelField(new GUIContent(
                    " Record on a From or To row copies it from the scene: click it, pose the object, then Finish.",
                    RecordIcon, ButtonTooltip), WrapStyle);
                return;
            }

            if (window != owner)
            {
                EditorGUILayout.HelpBox("'" + window.titleContent.text + "' is recording in another window. Finish or " +
                                        "Cancel it there to record here.", MessageType.Info);
                return;
            }

            UIAnimation animation = owner.Animation;

            // Deleted from under the recording: nothing is left to read or to put back. Rows whose step was moved,
            // retyped or pointed elsewhere since are dropped, rather than kept to be written into whatever step is
            // there now - and with none left there is nothing to record.
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                if (animation == null || !StillThere(animation, rows[i])) rows.RemoveAt(i);
            }

            if (player == null || rows.Count == 0)
            {
                Cancel();
                GUIUtility.ExitGUI();
            }

            ReadAll(animation, player, false, shown);

            Rect box = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(box, ArmedTint);

            EditorGUILayout.LabelField(new GUIContent(" Recording " + (rows.Count == 1 ? "1 value" : rows.Count + " values"),
                RecordIcon), EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "Pose the objects in the Scene view (or in their Inspector), then press Finish. Nothing is written " +
                "until then, and Finish and Cancel both put every object back. Click record on more rows to add them.",
                WrapStyle);

            string shared = SharedValues(animation);
            string edited = null;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                Rect line = EditorGUILayout.GetControlRect();

                UIAnimationStep step = animation.Steps[row.Step];
                UIAnimationEndpointMode mode = row.From ? step.FromMode : step.ToMode;
                if (!row.From && mode == UIAnimationEndpointMode.Current && step.UseFrom) mode = UIAnimationEndpointMode.Baseline;

                var labelRect = new Rect(line.x, line.y, Mathf.Min(150f, line.width * 0.42f), line.height);
                var removeRect = new Rect(line.xMax - 20f, line.y, 20f, line.height);
                var revealRect = new Rect(removeRect.x - 22f, line.y, 20f, line.height);
                var valueRect = new Rect(labelRect.xMax + 4f, line.y,
                    Mathf.Max(30f, revealRect.x - 4f - labelRect.xMax - 4f), line.height);

                EditorGUI.LabelField(labelRect, "Step " + row.Step + " " + (row.From ? "From" : "To") + " (" + mode + ")");
                DrawResult(valueRect, shown[i], mode != UIAnimationEndpointMode.Absolute);

                Object editable = EditableOf(step, player);
                bool material = UIAnimationStep.TargetKindOf(step.Type) == UIAnimationTargetKind.Material;

                using (new EditorGUI.DisabledScope(editable == null))
                {
                    RevealContent.tooltip = material
                        ? "Select the copy of the material this object shows while recording - change its values there. " +
                          "Changing the material asset itself shows nothing until recording ends."
                        : "Select the object that holds this value, and show it in the Inspector.";

                    if (GUI.Button(revealRect, RevealContent, IconButtonStyle))
                    {
                        UIAnimationReveal.Show(editable);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUI.Button(removeRect, new GUIContent("×", rows.Count == 1
                        ? "Stop recording this row. It is the only one, so this is the same as Cancel."
                        : "Stop recording this row."), EditorStyles.miniButton))
                {
                    Drop(i);
                    GUIUtility.ExitGUI();
                }

                if (edited == null && row.Source != null && ValueIn(row.Source, step) != row.SourceValue)
                {
                    edited = "The material asset '" + row.Source.name + "' was changed, but while recording, '" +
                             TargetNameOf(step, player) + "' shows a copy of it, and the copy is what gets recorded. Press the " +
                             "crosshair beside step " + row.Step + " to select the copy and change it there. The asset " +
                             "keeps the change - undo it there if it was not meant.";
                }
            }

            if (shared != null) EditorGUILayout.HelpBox(shared, MessageType.Warning);
            if (edited != null) EditorGUILayout.HelpBox(edited, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Cancel", "Puts every object back and writes nothing."), GUILayout.Width(80f)))
            {
                Cancel();
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Finish", "Writes the values shown, then puts every object back. One " +
                    "Undo step undoes the lot."), GUILayout.Width(80f)))
            {
                Finish();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private static string TargetNameOf(UIAnimationStep step, UIAnimationPlayer owner)
        {
            string problem;
            Object target = UIAnimationTargets.Resolve(step.Type, UIAnimationTargets.SlotOf(step, step.Type), step.TargetPath,
                owner.gameObject, out problem);

            return target != null ? target.name : "the object";
        }

        /// <summary>
        /// A warning naming the rows that read the same value, or null. They all get one pose - whatever it is on
        /// Finish - which is right for a step that starts where another ends and a surprise otherwise.
        /// </summary>
        private static string SharedValues(UIAnimation animation)
        {
            var keys = new List<string>();
            var labels = new List<string>();

            for (int i = 0; i < rows.Count; i++)
            {
                string key;
                string label;

                if (!TryValueOf(animation.Steps[rows[i].Step], player, out key, out label)) key = null;

                keys.Add(key);
                labels.Add(label);
            }

            var text = new System.Text.StringBuilder();
            var told = new HashSet<string>();

            for (int i = 0; i < rows.Count; i++)
            {
                if (keys[i] == null || told.Contains(keys[i])) continue;

                var names = new List<string>();
                for (int j = i; j < rows.Count; j++)
                {
                    if (keys[j] == keys[i]) names.Add("step " + rows[j].Step + " " + (rows[j].From ? "From" : "To"));
                }

                if (names.Count < 2) continue;
                told.Add(keys[i]);

                if (text.Length > 0) text.Append("\n\n");

                string list = names.Count == 2
                    ? names[0] + " and " + names[1]
                    : string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];

                text.Append(char.ToUpperInvariant(list[0])).Append(list.Substring(1)).Append(names.Count == 2 ? " both" : " all")
                    .Append(" record ").Append(labels[i]).Append(", so they get the same value: whatever it is when you ")
                    .Append("press Finish.");
            }

            if (text.Length == 0) return null;

            text.Append(" That is right for a step that should start where another ends. To give them different values, " +
                        "record them one at a time.");
            return text.ToString();
        }
    }

    /// <summary>
    /// Selects an object and brings it into view in the Inspector, for the recording panel's crosshair button. A
    /// component's GameObject is selected, the component unfolded, and every Inspector showing it scrolled to it -
    /// which Unity offers no call for, so it is done through the Inspector's own scroll view by reflection, and
    /// simply skipped if that ever stops working. Anything else (a material) is selected as it is.
    /// </summary>
    internal static class UIAnimationReveal
    {
        private static Component pending;
        private static double giveUpAt;

        public static void Show(Object target)
        {
            if (target == null) return;

            var component = target as Component;
            if (component == null)
            {
                Selection.activeObject = target;
                return;
            }

            Selection.activeGameObject = component.gameObject;
            InternalEditorUtility.SetIsInspectorExpanded(component, true);
            EditorGUIUtility.PingObject(component.gameObject);

            // The Inspector rebuilds for the new selection on a later update, and only lays the result out after.
            pending = component;
            giveUpAt = EditorApplication.timeSinceStartup + 1.0;

            EditorApplication.update -= Scroll;
            EditorApplication.update += Scroll;
        }

        private static void Scroll()
        {
            if (pending == null || EditorApplication.timeSinceStartup > giveUpAt)
            {
                EditorApplication.update -= Scroll;
                pending = null;
                return;
            }

            try
            {
                if (TryScrollTo(pending))
                {
                    EditorApplication.update -= Scroll;
                    pending = null;
                }
            }
            catch (System.Exception)
            {
                // Unity's internals changed. Selecting and unfolding it still happened.
                EditorApplication.update -= Scroll;
                pending = null;
            }
        }

        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private static bool TryScrollTo(Component component)
        {
            System.Type inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            System.Type propertyEditorType = typeof(Editor).Assembly.GetType("UnityEditor.PropertyEditor");
            if (inspectorType == null || propertyEditorType == null) return true;

            FieldInfo scrollField = propertyEditorType.GetField("m_ScrollView", Hidden);
            FieldInfo editorsField = propertyEditorType.GetField("m_EditorsElement", Hidden);
            if (scrollField == null || editorsField == null) return true;

            bool any = false;

            foreach (Object found in Resources.FindObjectsOfTypeAll(inspectorType))
            {
                var scrollView = scrollField.GetValue(found) as UnityEngine.UIElements.ScrollView;
                var editors = editorsField.GetValue(found) as UnityEngine.UIElements.VisualElement;
                if (scrollView == null || editors == null) continue;

                foreach (UnityEngine.UIElements.VisualElement element in editors.Children())
                {
                    if (!Shows(element, component)) continue;

                    // Not laid out yet: try again next update.
                    if (float.IsNaN(element.layout.height) || element.layout.height <= 0f) return false;

                    Vector2 top = UnityEngine.UIElements.VisualElementExtensions.ChangeCoordinatesTo(element, scrollView.contentContainer, Vector2.zero);
                    scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, Mathf.Max(0f, top.y));
                    any = true;
                }
            }

            return any;
        }

        private static bool Shows(UnityEngine.UIElements.VisualElement element, Component component)
        {
            PropertyInfo editorsProperty = element.GetType().GetProperty("Editors", Hidden);
            var editors = editorsProperty != null ? editorsProperty.GetValue(element) as Editor[] : null;
            if (editors == null) return false;

            for (int i = 0; i < editors.Length; i++)
            {
                if (editors[i] != null && editors[i].target == component) return true;
            }

            return false;
        }
    }
}
