// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, October 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// One animation in a window of its own, opened with the Pop Out button on its header in a player's or
    /// an Animation Set's Inspector. It is an ordinary dockable window and stays open whatever is selected,
    /// which is the point: the objects the animation moves can be selected and posed while it stays on
    /// screen. It is also the only place with record buttons (UIAnimationRecording).
    ///
    /// The Inspector it came from shows the animation as one line with a Focus button while it is open, so
    /// the same fields are never edited in two places at once. The window draws the animation's Scene
    /// gizmos and endpoint handles itself, whatever is selected, and owns any preview it starts, so
    /// selecting other objects does not end it either.
    ///
    /// It remembers the animation by its place in the list and its name, and finds it again when either
    /// changes - moved under its name, or renamed where it stands - and says so when it is gone.
    /// </summary>
    internal sealed class UIAnimationWindow : EditorWindow
    {
        [SerializeField] private Object target;

        // The target's GlobalObjectId, so the window finds it again after its scene is closed and reopened,
        // which gives every object in it a new instance.
        [SerializeField] private string targetId;

        [SerializeField] private int index;
        [SerializeField] private string animationName;

        // How long the list was when the animation was last found, to tell a rename in place from a removal.
        [SerializeField] private int listLength;

        [SerializeField] private Vector2 scroll;

        [System.NonSerialized] private SerializedObject serialized;
        [System.NonSerialized] private int seenDirtyCount = -1;
        [System.NonSerialized] private double lastSearch;

        private static readonly List<UIAnimationWindow> open = new List<UIAnimationWindow>();
        private static readonly Dictionary<SerializedObject, UIAnimationWindow> byObject = new Dictionary<SerializedObject, UIAnimationWindow>();

        // ---------------------------------------------------------------- lookups

        /// <summary>Opens the animation at index in a player's or a set's Animations, or brings its window forward.</summary>
        public static void Open(Object target, int index)
        {
            List<UIAnimation> list = AnimationsOf(target);
            if (list == null || index < 0 || index >= list.Count || list[index] == null) return;

            UIAnimationWindow existing = Find(target, index);
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<UIAnimationWindow>();
            window.target = target;
            window.targetId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
            window.index = index;
            window.animationName = list[index].Name;
            window.listLength = list.Count;
            window.UpdateTitle();

            window.Show();

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float height = Mathf.Min(760f, main.height - 160f);
            window.position = new Rect(main.x + main.width * 0.5f - 240f, main.y + 90f, 480f, height);
            window.Focus();
        }

        /// <summary>The open window showing this animation, or null.</summary>
        public static UIAnimationWindow Find(Object target, int index)
        {
            if (target == null) return null;

            for (int i = 0; i < open.Count; i++)
            {
                UIAnimationWindow window = open[i];
                if (window != null && window.target == target && window.Locate() && window.index == index) return window;
            }

            return null;
        }

        /// <summary>The window drawing with this SerializedObject, or null - the Inspector's are never a window's.</summary>
        public static UIAnimationWindow Of(SerializedObject serialized)
        {
            UIAnimationWindow window;
            return serialized != null && byObject.TryGetValue(serialized, out window) ? window : null;
        }

        /// <summary>The animation list of a player (its own) or a set.</summary>
        private static List<UIAnimation> AnimationsOf(Object target)
        {
            var player = target as UIAnimationPlayer;
            if (player != null) return player.EditorAnimations;

            var asset = target as UIAnimationAsset;
            return asset != null ? asset.Animations : null;
        }

        // ---------------------------------------------------------------- what the window shows

        /// <summary>What the window edits through, made on first use. Null while the target is gone.</summary>
        public SerializedObject Serialized
        {
            get { return EnsureSerialized() ? serialized : null; }
        }

        public string AnimationName
        {
            get { return animationName; }
        }

        public string StepPath(int step)
        {
            return "Animations.Array.data[" + index + "].Steps.Array.data[" + step + "]";
        }

        /// <summary>The animation itself, or null when it is gone.</summary>
        public UIAnimation Animation
        {
            get { return Locate() ? AnimationsOf(target)[index] : null; }
        }

        /// <summary>The player whose scene the animation's steps resolve against and record from, or null.</summary>
        public UIAnimationPlayer Owner
        {
            get { return UIAnimationTargets.OwnerOf(Serialized); }
        }

        /// <summary>The step a property of this window's SerializedObject is, if it is one of this animation's.</summary>
        public bool TryGetStep(SerializedProperty step, out UIAnimation animation, out UIAnimationStep data, out int stepIndex)
        {
            animation = null;
            data = null;

            int animationIndex;
            if (!UIAnimationTargets.TryParseStepPath(step.propertyPath, out animationIndex, out stepIndex)
                || animationIndex != index || !Locate())
            {
                return false;
            }

            animation = AnimationsOf(target)[index];
            if (stepIndex < 0 || stepIndex >= animation.Steps.Count) return false;

            data = animation.Steps[stepIndex];
            return data != null;
        }

        /// <summary>
        /// Finds the animation again after the list changed: where it was under the same name, elsewhere under
        /// that name (the nearest, if two share it), or renamed where it stands while the list kept its length.
        /// False when it is gone.
        /// </summary>
        private bool Locate()
        {
            List<UIAnimation> list = AnimationsOf(target);
            if (list == null) return false;

            if (index >= 0 && index < list.Count && list[index] != null && list[index].Name == animationName)
            {
                listLength = list.Count;
                return true;
            }

            int found = -1;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || list[i].Name != animationName) continue;
                if (found < 0 || Mathf.Abs(i - index) < Mathf.Abs(found - index)) found = i;
            }

            if (found < 0)
            {
                if (index < 0 || index >= list.Count || list.Count != listLength || list[index] == null) return false;

                found = index;
            }

            index = found;
            animationName = list[found].Name;
            listLength = list.Count;
            UpdateTitle();
            return true;
        }

        private void UpdateTitle()
        {
            Texture icon = EditorGUIUtility.FindTexture(EditorGUIUtility.isProSkin ? "d_AnimationClip Icon" : "AnimationClip Icon");
            titleContent = new GUIContent(string.IsNullOrEmpty(animationName) ? "Animation" : animationName, icon,
                target != null ? animationName + " on '" + target.name + "'" : animationName);
        }

        // ---------------------------------------------------------------- lifecycle

        private void OnEnable()
        {
            if (!open.Contains(this)) open.Add(this);

            SceneView.duringSceneGui += DrawInScene;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawInScene;
            Undo.undoRedoPerformed -= Repaint;

            // Closing the window keeps what was recorded - it is the Finish button, not Cancel. A recompile or
            // play mode has already ended the session by the time this runs. A preview it started is stopped and
            // restored whatever is selected: closing it is a deliberate act, which a selection change is not.
            if (UIAnimationRecording.IsRecording(this)) UIAnimationRecording.Finish();
            else UIAnimationPreview.EndIfOwnedBy(this, true);

            open.Remove(this);
            ForgetSerialized();

            // The Inspector shows this animation as one line while the window is open.
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void ForgetSerialized()
        {
            if (serialized == null) return;

            byObject.Remove(serialized);
            serialized.Dispose();
            serialized = null;
        }

        /// <summary>
        /// Keeps the window current with edits made elsewhere - the Inspector, the Scene view, Undo - and live
        /// while a preview or a recording is moving things. Ten times a second, and only repainting on a change.
        /// </summary>
        private void OnInspectorUpdate()
        {
            if (target == null)
            {
                if (EditorApplication.timeSinceStartup - lastSearch > 1.0)
                {
                    lastSearch = EditorApplication.timeSinceStartup;
                    if (Recover()) Repaint();
                }

                return;
            }

            UIAnimationPlayer owner = Owner;

            if (UIAnimationRecording.IsRecording(this) || (owner != null && UIAnimationPreview.IsPreviewing(owner)))
            {
                Repaint();
                return;
            }

            int dirty = EditorUtility.GetDirtyCount(target);
            if (dirty != seenDirtyCount)
            {
                seenDirtyCount = dirty;
                Repaint();
            }
        }

        /// <summary>Finds the target again by its GlobalObjectId, after its scene was closed and reopened.</summary>
        private bool Recover()
        {
            GlobalObjectId id;
            if (string.IsNullOrEmpty(targetId) || !GlobalObjectId.TryParse(targetId, out id)) return false;

            Object found = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
            if (found == null || AnimationsOf(found) == null) return false;

            target = found;
            ForgetSerialized();
            return true;
        }

        private bool EnsureSerialized()
        {
            if (target == null) return false;

            if (serialized == null || serialized.targetObject != target)
            {
                ForgetSerialized();
                serialized = new SerializedObject(target);
                byObject[serialized] = this;
            }

            return true;
        }

        // ---------------------------------------------------------------- drawing

        private void OnGUI()
        {
            if (target == null) Recover();

            if (!EnsureSerialized())
            {
                DrawGone("The object this animation belongs to is gone - deleted, or its scene is closed. The window " +
                         "finds it again if the scene is reopened.");
                return;
            }

            if (!Locate())
            {
                DrawGone("'" + animationName + "' is no longer in '" + target.name + "'.");
                return;
            }

            serialized.Update();

            SerializedProperty animation = serialized.FindProperty("Animations.Array.data[" + index + "]");
            if (animation == null)
            {
                DrawGone("'" + animationName + "' is no longer in '" + target.name + "'.");
                return;
            }

            // Laid out the way the Inspector lays out fields, so the steps look and measure the same here.
            float labelWidth = EditorGUIUtility.labelWidth;
            bool hierarchyMode = EditorGUIUtility.hierarchyMode;
            bool wideMode = EditorGUIUtility.wideMode;

            EditorGUIUtility.hierarchyMode = true;
            EditorGUIUtility.wideMode = position.width > 330f;
            EditorGUIUtility.labelWidth = Mathf.Max(position.width * 0.45f - 40f, 120f);

            try
            {
                EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);
                EditorGUILayout.Space(4f);

                // Pinned above everything else, so what is being recorded and Finish are always in sight.
                UIAnimationRecording.DrawPanel(this);

                DrawSource();
                DrawPreview();

                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(2f);
                Rect rule = EditorGUILayout.GetControlRect(false, 1f);
                if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(rule, new Color(0f, 0f, 0f, 0.25f));

                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.BeginVertical(EditorStyles.inspectorDefaultMargins);

                DrawFields(animation);

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
                EditorGUIUtility.hierarchyMode = hierarchyMode;
                EditorGUIUtility.wideMode = wideMode;
            }

            serialized.ApplyModifiedProperties();
        }

        private void DrawGone(string message)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(message, MessageType.Info);

            if (GUILayout.Button("Close")) Close();
        }

        /// <summary>Where the animation lives - with Preview On beside it for a set - and a button to select it.</summary>
        private void DrawSource()
        {
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(target is UIAnimationAsset ? "Animation Set" : "Player", target,
                    typeof(Object), true);
            }

            if (GUILayout.Button(new GUIContent("Select", "Selects it, so the Inspector shows the rest of it."),
                    EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }

            EditorGUILayout.EndHorizontal();

            var asset = target as UIAnimationAsset;
            if (asset == null) return;

            UIAnimationPlayer chosen = UIAnimationAssetEditor.ChosenPreviewPlayer(asset);

            EditorGUI.BeginChangeCheck();
            chosen = (UIAnimationPlayer)EditorGUILayout.ObjectField(new GUIContent("Preview On",
                    "A UI Animation Player in the open scene that uses this set - the objects the animation is previewed " +
                    "on, drawn on in the Scene view and recorded from. The same slot as the set's own Inspector has."),
                chosen, typeof(UIAnimationPlayer), true);

            if (EditorGUI.EndChangeCheck()) UIAnimationAssetEditor.SetPreviewPlayer(asset, chosen);

            if (chosen != null && chosen.EditorShared != asset)
            {
                EditorGUILayout.HelpBox("'" + chosen.name + "' does not use this set, so nothing can be previewed or " +
                                        "recorded on it. Pick a player whose Shared Anim. Asset is '" + asset.name + "'.",
                    MessageType.Warning);
            }
            else if (chosen != null && HasLocal(chosen, animationName))
            {
                EditorGUILayout.HelpBox("'" + chosen.name + "' has its own animation called '" + animationName +
                                        "', so Play below plays that one instead. Recording still records into this one.",
                    MessageType.Warning);
            }
        }

        private static bool HasLocal(UIAnimationPlayer player, string name)
        {
            List<UIAnimation> local = player.EditorAnimations;

            for (int i = 0; i < local.Count; i++)
            {
                if (local[i] != null && local[i].Name == name) return true;
            }

            return false;
        }

        /// <summary>The animation's Play and Reset, Stop and Restore, and the Scene gizmo settings.</summary>
        private void DrawPreview()
        {
            UIAnimationPlayer owner = Owner;
            if (owner == null || string.IsNullOrEmpty(animationName)) return;

            UIAnimationPreview.DrawRow(this, owner, animationName);

            // While recording, Finish and Cancel end the preview, and say what happens to the pose.
            if (!UIAnimationRecording.IsRecording(this)) UIAnimationPreview.DrawFooter(owner);

            if (!Application.isPlaying) UIAnimationGizmos.DrawButton();
        }

        /// <summary>Every field of the animation, as the Inspector draws them under its header.</summary>
        private static void DrawFields(SerializedProperty animation)
        {
            SerializedProperty child = animation.Copy();
            SerializedProperty end = animation.GetEndProperty();
            bool enter = true;

            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;
                EditorGUILayout.PropertyField(child, true);
            }
        }

        private void DrawInScene(SceneView view)
        {
            if (!EnsureSerialized() || !Locate()) return;

            UIAnimationPlayer owner = Owner;
            if (owner != null) UIAnimationGizmos.Draw(serialized, owner, index);
        }
    }
}
