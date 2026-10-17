// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// An animation in a list: Unity's own foldout-and-fields layout, row for row, plus a small note at
    /// the end of the header row when a player's own animation and one in its shared set share a name -
    /// "overrides shared" on the player's, "overridden locally" on the set's.
    ///
    /// Inside a player's Shared Animations (UIAnimationSharedSection) it also greys every field out,
    /// puts an Override button where the note would be, and adds this player's own On Complete below.
    ///
    /// Everywhere else the header ends in a Pop Out button, which opens the animation in a window of its
    /// own (UIAnimationWindow). While that window is open, the animation here is one line saying so.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIAnimation))]
    internal class UIAnimationDrawer : PropertyDrawer
    {
        private const string OnCompleteLabel = "On Complete On This Player";

        private const string OnCompleteTooltip =
            "Runs when this animation finishes on this player, after the set's own On Complete. Other players " +
            "using the set keep their own. Kept if the animation is renamed in the set.";

        private static GUIStyle noteStyle;

        private static GUIStyle NoteStyle
        {
            get
            {
                if (noteStyle == null)
                {
                    noteStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                    Color color = noteStyle.normal.textColor;
                    color.a *= 0.7f;
                    noteStyle.normal.textColor = color;
                }

                return noteStyle;
            }
        }

        private const string PopOutTooltip =
            "Opens this animation in a window of its own, where its From and To values can be recorded from the " +
            "scene. The window stays open and keeps drawing the animation in the Scene view whatever is selected, " +
            "so the objects it moves can be selected and posed. Here it shows as one line until the window closes.";

        private static GUIStyle windowedStyle;

        /// <summary>A label faded like DimFoldout's text, for an animation open in a window: a name with no arrow to click.</summary>
        private static GUIStyle WindowedStyle
        {
            get
            {
                if (windowedStyle == null)
                {
                    windowedStyle = new GUIStyle(EditorStyles.label);
                    Color color = windowedStyle.normal.textColor;
                    color.a *= 0.45f;
                    windowedStyle.normal.textColor = color;
                }

                return windowedStyle;
            }
        }

        private static GUIStyle dimFoldout;

        /// <summary>The foldout with its text faded, for a shared animation this player never plays.</summary>
        private static GUIStyle DimFoldout
        {
            get
            {
                if (dimFoldout == null)
                {
                    dimFoldout = new GUIStyle(EditorStyles.foldout);

                    foreach (GUIStyleState state in new[]
                             {
                                 dimFoldout.normal, dimFoldout.onNormal, dimFoldout.hover, dimFoldout.onHover,
                                 dimFoldout.focused, dimFoldout.onFocused, dimFoldout.active, dimFoldout.onActive,
                             })
                    {
                        Color color = state.textColor;
                        color.a *= 0.45f;
                        state.textColor = color;
                    }
                }

                return dimFoldout;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded || WindowOf(property) != null) return line;

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            UIAnimationPlayer viewer = UIAnimationSharedSection.PlayerOf(property.serializedObject);

            float height = line;
            List<SerializedProperty> children = Children(property, viewer != null);

            for (int i = 0; i < children.Count; i++)
            {
                height += spacing + EditorGUI.GetPropertyHeight(children[i], true);
            }

            if (viewer != null && ShowsPlayerOnComplete(property, viewer)) height += spacing + PlayerOnCompleteHeight(property);

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            UIAnimationPlayer viewer = UIAnimationSharedSection.PlayerOf(property.serializedObject);
            string name = property.FindPropertyRelative("Name").stringValue;

            EditorGUI.BeginProperty(position, label, property);

            Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            UIAnimationWindow window = viewer == null ? WindowOf(property) : null;
            if (window != null)
            {
                DrawWindowed(header, new GUIContent(string.IsNullOrEmpty(name) ? label.text : name, label.tooltip), window);
                EditorGUI.EndProperty();
                return;
            }

            bool overridden;
            float side = DrawSide(header, property, viewer, name, out overridden);

            Rect foldout = new Rect(header.x, header.y, Mathf.Max(0f, header.width - side), header.height);
            var content = new GUIContent(string.IsNullOrEmpty(name) ? label.text : name, label.tooltip);

            property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, content, true,
                viewer != null && overridden ? DimFoldout : EditorStyles.foldout);

            if (property.isExpanded) DrawChildren(position, property, viewer);

            EditorGUI.EndProperty();
        }

        // ---------------------------------------------------------------- header note

        /// <summary>
        /// The pop-up window showing this animation, or null. Only a player's own animations and a set's can
        /// be popped out - a player's Shared Animations are read-only - and only with one object selected.
        /// </summary>
        private static UIAnimationWindow WindowOf(SerializedProperty property)
        {
            int index;
            return CanPopOut(property, out index) ? UIAnimationWindow.Find(property.serializedObject.targetObject, index) : null;
        }

        private static bool CanPopOut(SerializedProperty property, out int index)
        {
            index = -1;

            SerializedObject serialized = property.serializedObject;
            if (serialized.isEditingMultipleObjects || UIAnimationSharedSection.PlayerOf(serialized) != null) return false;
            if (!(serialized.targetObject is UIAnimationPlayer) && !(serialized.targetObject is UIAnimationAsset)) return false;

            const string head = "Animations.Array.data[";
            string path = property.propertyPath;

            return path.StartsWith(head, System.StringComparison.Ordinal) && path.EndsWith("]", System.StringComparison.Ordinal)
                && int.TryParse(path.Substring(head.Length, path.Length - head.Length - 1), out index);
        }

        /// <summary>
        /// The one line an animation open in a window gets: its name, a note, and Focus. It is only edited in
        /// the window while that is open, so the same fields are never being changed in two places.
        /// </summary>
        private static void DrawWindowed(Rect header, GUIContent name, UIAnimationWindow window)
        {
            var focus = new GUIContent("Focus", "Brings its window to the front.");
            float buttonWidth = EditorStyles.miniButton.CalcSize(focus).x + 8f;
            var button = new Rect(header.xMax - buttonWidth, header.y + 1f, buttonWidth, header.height - 2f);

            if (GUI.Button(button, focus, EditorStyles.miniButton)) window.Focus();

            var note = new GUIContent("open in a window", "It is edited in its own window while that is open. Close the " +
                                                          "window to edit it here again.");
            float noteWidth = NoteStyle.CalcSize(note).x;
            GUI.Label(new Rect(button.x - noteWidth - 4f, header.y, noteWidth, header.height), note, NoteStyle);

            // Past where the arrow would be, so the name lines up with the other animations' names.
            float arrow = EditorStyles.foldout.padding.left - EditorStyles.label.padding.left;
            var text = new Rect(header.x + arrow, header.y, Mathf.Max(0f, button.x - noteWidth - 8f - header.x - arrow), header.height);
            GUI.Label(EditorGUI.IndentedRect(text), name, WindowedStyle);
        }

        private static float DrawPopOutButton(Rect header, Object target, int index)
        {
            var content = new GUIContent("Pop Out", PopOutTooltip);
            float width = EditorStyles.miniButton.CalcSize(content).x + 8f;
            var rect = new Rect(header.xMax - width, header.y + 1f, width, header.height - 2f);

            if (GUI.Button(rect, content, EditorStyles.miniButton))
            {
                UIAnimationWindow.Open(target, index);
                GUIUtility.ExitGUI();
            }

            return width + 4f;
        }

        /// <summary>
        /// The note or button at the end of the header row. Returns how much of the row it took. Drawn
        /// before the foldout, which would otherwise take the button's clicks - the whole row toggles it.
        /// </summary>
        private static float DrawSide(Rect header, SerializedProperty property, UIAnimationPlayer viewer, string name,
            out bool overridden)
        {
            overridden = false;
            string note = null;
            string tooltip = null;

            // Pop Out sits at the very end, with any note to its left.
            float taken = 0f;
            int index;

            if (viewer == null && CanPopOut(property, out index))
            {
                taken = DrawPopOutButton(header, property.serializedObject.targetObject, index);
                header.xMax -= taken;
            }

            var owner = property.serializedObject.targetObject as UIAnimationPlayer;
            var asset = property.serializedObject.targetObject as UIAnimationAsset;

            if (viewer != null)
            {
                overridden = HasLocal(viewer, name);
                if (overridden)
                {
                    note = "overridden locally";
                    tooltip = "'" + viewer.name + "' has its own animation called '" + name + "', which plays instead of this one.";
                }
                else
                {
                    return DrawOverrideButton(header, property, viewer);
                }
            }
            else if (owner != null && !property.serializedObject.isEditingMultipleObjects)
            {
                if (owner.EditorShared != null && InSet(owner.EditorShared, name))
                {
                    note = "overrides shared";
                    tooltip = "'" + owner.EditorShared.name + "' also has an animation called '" + name +
                              "'. This one plays instead of it on this player.";
                }
            }
            else if (asset != null)
            {
                UIAnimationPlayer preview = UIAnimationAssetEditor.PreviewPlayerFor(asset);
                if (preview != null && HasLocal(preview, name))
                {
                    note = "overridden on '" + preview.name + "'";
                    tooltip = "'" + preview.name + "' has its own animation called '" + name +
                              "', so previewing this one on it plays that instead.";
                }
            }

            if (note == null) return taken;

            var content = new GUIContent(note, tooltip);
            float width = NoteStyle.CalcSize(content).x;

            GUI.Label(new Rect(header.xMax - width, header.y, width, header.height), content, NoteStyle);
            return taken + width + 4f;
        }

        private static float DrawOverrideButton(Rect header, SerializedProperty property, UIAnimationPlayer viewer)
        {
            var content = new GUIContent("Override",
                "Copies this animation into '" + viewer.name + "''s own Animations, where it can be edited and plays " +
                "instead of the set's. This player's On Complete for it moves with it. Other players are unaffected.");

            float width = EditorStyles.miniButton.CalcSize(content).x + 8f;
            var rect = new Rect(header.xMax - width, header.y + 1f, width, header.height - 2f);

            if (GUI.Button(rect, content, EditorStyles.miniButton))
            {
                SerializedObject serialized = UIAnimationSharedSection.PlayerSerializedOf(property.serializedObject);
                if (serialized != null)
                {
                    UIAnimationSharedSection.Override(viewer, serialized, ElementIndex(property));
                    GUIUtility.ExitGUI();
                }
            }

            return width + 4f;
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

        private static bool InSet(UIAnimationAsset asset, string name)
        {
            for (int i = 0; i < asset.Animations.Count; i++)
            {
                if (asset.Animations[i] != null && asset.Animations[i].Name == name) return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- fields

        /// <summary>
        /// Every field below the header, the way Unity lays out a class's fields. Greyed out inside a
        /// player's Shared Animations - Foldouts still open while greyed out, so steps can still be read -
        /// with this player's own On Complete after them, which is not.
        /// </summary>
        private static void DrawChildren(Rect position, SerializedProperty property, UIAnimationPlayer viewer)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + spacing, position.width, 0f);

            List<SerializedProperty> children = Children(property, viewer != null);

            EditorGUI.indentLevel++;

            using (new EditorGUI.DisabledScope(viewer != null))
            {
                for (int i = 0; i < children.Count; i++)
                {
                    SerializedProperty child = children[i];
                    row.height = EditorGUI.GetPropertyHeight(child, true);

                    if (viewer != null && child.name == "OnComplete")
                    {
                        EditorGUI.PropertyField(row, child, new GUIContent("On Complete In The Set", child.tooltip), true);
                    }
                    else
                    {
                        EditorGUI.PropertyField(row, child, true);
                    }

                    row.y += row.height + spacing;
                }
            }

            if (viewer != null && ShowsPlayerOnComplete(property, viewer))
            {
                row.height = PlayerOnCompleteHeight(property);
                DrawPlayerOnComplete(row, property, viewer);
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Whether this player's own On Complete row is shown. Not for an animation the player overrides,
        /// which never plays here - unless one is already there (a copy made by hand rather than with
        /// Override, which moves it), when it is shown greyed out rather than hidden.
        /// </summary>
        private static bool ShowsPlayerOnComplete(SerializedProperty property, UIAnimationPlayer viewer)
        {
            return !HasLocal(viewer, property.FindPropertyRelative("Name").stringValue) || EntryOf(property) != null;
        }

        /// <summary>
        /// The visible fields under an animation, in order. Inside Shared Animations the set's own On
        /// Complete is left out while it is empty, which is nearly always: a set can only hold listeners
        /// on other assets, and an empty event box there would only be confused with this player's.
        /// </summary>
        private static List<SerializedProperty> Children(SerializedProperty property, bool shared)
        {
            var children = new List<SerializedProperty>();

            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enter = true;

            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            {
                enter = false;

                if (shared && child.name == "OnComplete" && ListenerCount(child) == 0) continue;

                children.Add(child.Copy());
            }

            return children;
        }

        private static int ListenerCount(SerializedProperty unityEvent)
        {
            SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
            return calls != null ? calls.arraySize : 0;
        }

        // ---------------------------------------------------------------- this player's On Complete

        private static float PlayerOnCompleteHeight(SerializedProperty property)
        {
            SerializedProperty entry = EntryOf(property);
            return entry != null
                ? EditorGUI.GetPropertyHeight(entry.FindPropertyRelative("OnComplete"), true)
                : EditorGUIUtility.singleLineHeight;
        }

        private static void DrawPlayerOnComplete(Rect rect, SerializedProperty property, UIAnimationPlayer viewer)
        {
            SerializedProperty entry = EntryOf(property);

            if (entry == null)
            {
                Rect button = EditorGUI.IndentedRect(rect);

                if (GUI.Button(button, new GUIContent("Add " + OnCompleteLabel, OnCompleteTooltip), EditorStyles.miniButton))
                {
                    SerializedObject serialized = UIAnimationSharedSection.PlayerSerializedOf(property.serializedObject);
                    if (serialized != null)
                    {
                        UIAnimationSharedSection.AddOnComplete(viewer, serialized, ElementIndex(property));
                        GUIUtility.ExitGUI();
                    }
                }

                return;
            }

            bool unused = HasLocal(viewer, property.FindPropertyRelative("Name").stringValue);

            // The name it goes by is refreshed on any edit, so an entry whose animation later leaves the
            // set can still be named.
            EditorGUI.BeginChangeCheck();

            using (new EditorGUI.DisabledScope(unused))
            {
                EditorGUI.PropertyField(rect, entry.FindPropertyRelative("OnComplete"),
                    unused
                        ? new GUIContent(OnCompleteLabel + " (unused: overridden locally)",
                            "This player plays its own animation of this name instead, so this never runs. It is kept " +
                            "in case the local one is removed.")
                        : new GUIContent(OnCompleteLabel, OnCompleteTooltip),
                    true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                entry.FindPropertyRelative("AnimationName").stringValue = property.FindPropertyRelative("Name").stringValue;
            }
        }

        /// <summary>This player's On Complete entry for the set's animation drawn by property, or null.</summary>
        private static SerializedProperty EntryOf(SerializedProperty property)
        {
            SerializedObject serialized = UIAnimationSharedSection.PlayerSerializedOf(property.serializedObject);
            return UIAnimationSharedSection.EntryFor(serialized, property.FindPropertyRelative("Id").stringValue);
        }

        /// <summary>The index out of "Animations.Array.data[n]".</summary>
        private static int ElementIndex(SerializedProperty property)
        {
            string path = property.propertyPath;
            int open = path.LastIndexOf('[');
            int index;

            return open >= 0 && int.TryParse(path.Substring(open + 1, path.Length - open - 2), out index) ? index : -1;
        }
    }
}
