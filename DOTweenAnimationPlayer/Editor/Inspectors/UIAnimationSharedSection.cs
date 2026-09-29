// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// "Shared Animations", under a player's own Animations in its Inspector: every animation its Shared
    /// Anim. Asset adds, always as the set has it right now.
    ///
    /// They are read-only here - the set is edited in the set's own Inspector, and an edit made from one
    /// player would change every player using it. Two things are this player's own: an On Complete per
    /// animation (UIAnimationPlayer's SharedOnComplete), which runs after the set's own, and Override,
    /// which copies the animation into the player's own list, where it wins over the set's and can be
    /// edited. This player's On Complete goes with it.
    ///
    /// What is drawn is a private copy of the set, refreshed whenever the set changes, so nothing here
    /// can write to the set itself - not a control that slips past the greying out, and not Unity's
    /// own right-click entries such as Delete Array Element, which apply their change themselves (they
    /// are taken off the menu as well; see UIAnimationContextMenu). The step drawer checks the steps
    /// against this player (UIAnimationTargets.OwnerOf asks PlayerOf), not the set's Preview On player,
    /// since this is the scene they will run in.
    /// </summary>
    internal static class UIAnimationSharedSection
    {
        private const string Tooltip =
            "The animations this player's Shared Anim. Asset adds, as the set has them now. Read-only here: " +
            "edit the set itself, or press Override to copy one into this player's own Animations, where it " +
            "wins over the set's.\n\n" +
            "Each can have its own On Complete on this player, which runs after the set's.";

        private sealed class View
        {
            public UIAnimationPlayer Player;
            public UIAnimationAsset Asset;
            public UIAnimationAsset Copy;
            public int CopiedAt;
            public SerializedObject Set;
            public SerializedObject PlayerSerialized;
            public ReorderableList List;
        }

        // One view of the set per player, kept between repaints so what is expanded stays expanded, and
        // looked up by the SerializedObject the drawers are handed.
        private static readonly Dictionary<UIAnimationPlayer, View> views = new Dictionary<UIAnimationPlayer, View>();
        private static readonly Dictionary<SerializedObject, View> byObject = new Dictionary<SerializedObject, View>();

        /// <summary>
        /// The player whose Shared Animations a serialized object is being drawn in, or null when it is
        /// an ordinary one - a player, or a set in its own Inspector.
        /// </summary>
        public static UIAnimationPlayer PlayerOf(SerializedObject serialized)
        {
            View view;
            return serialized != null && byObject.TryGetValue(serialized, out view) ? view.Player : null;
        }

        /// <summary>The player's own SerializedObject, as last drawn, for a set drawn in its section.</summary>
        public static SerializedObject PlayerSerializedOf(SerializedObject serialized)
        {
            View view;
            return serialized != null && byObject.TryGetValue(serialized, out view) ? view.PlayerSerialized : null;
        }

        /// <summary>
        /// The player's view of its set, which says which shared animations and steps are expanded, for the
        /// Scene-view gizmos. Null before the section has been drawn.
        /// </summary>
        public static SerializedObject ViewOf(UIAnimationPlayer player)
        {
            View view;
            return player != null && views.TryGetValue(player, out view) && view.Asset == player.EditorShared ? view.Set : null;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>The section itself. serialized is the player Inspector's own SerializedObject.</summary>
        public static void Draw(UIAnimationPlayer player, SerializedObject serialized)
        {
            Prune();

            UIAnimationAsset asset = player.EditorShared;

            if (asset != null)
            {
                View view = ViewFor(player, asset);
                view.PlayerSerialized = serialized;

                // Every edit to the set dirties it, Undo included, so this is how the copy stays current.
                int changes = EditorUtility.GetDirtyCount(asset);
                if (changes != view.CopiedAt)
                {
                    EditorUtility.CopySerialized(asset, view.Copy);
                    view.CopiedAt = changes;
                }

                view.Set.Update();

                DrawList(view);
            }

            DrawOrphans(player, serialized, asset);
        }

        private static View ViewFor(UIAnimationPlayer player, UIAnimationAsset asset)
        {
            View view;
            if (views.TryGetValue(player, out view) && view.Asset == asset && view.Copy != null) return view;

            if (view != null) Forget(player, view);

            UIAnimationAsset copy = Object.Instantiate(asset);
            copy.name = asset.name;
            // DontSave, and not HideAndDontSave: that includes NotEditable, and Unity greys out every
            // property of a NotEditable object - this player's own On Complete and the Override button
            // included, since they are drawn inside the animation's BeginProperty. The fields that must
            // not change are greyed out by UIAnimationDrawer instead.
            copy.hideFlags = HideFlags.DontSave;

            view = new View
            {
                Player = player,
                Asset = asset,
                Copy = copy,
                CopiedAt = EditorUtility.GetDirtyCount(asset),
                Set = new SerializedObject(copy),
            };

            SerializedProperty animations = view.Set.FindProperty("Animations");

            // Read-only: no dragging, no + or -. Its elements draw through UIAnimationDrawer, which greys
            // them out and adds this player's own parts.
            view.List = new ReorderableList(view.Set, animations, false, true, false, false)
            {
                footerHeight = 0f,
                drawFooterCallback = rect => { },
                drawHeaderCallback = rect => DrawHeader(rect, animations, asset),
                elementHeightCallback = index =>
                    EditorGUI.GetPropertyHeight(animations.GetArrayElementAtIndex(index), true) + ElementPadding,
                drawElementCallback = (rect, index, active, focused) =>
                {
                    SerializedProperty element = animations.GetArrayElementAtIndex(index);

                    // Room for the foldout arrow, as Unity's own lists leave.
                    rect.xMin += FoldoutMargin;
                    rect.y += ElementPadding / 2f;
                    rect.height -= ElementPadding;

                    EditorGUI.PropertyField(rect, element, new GUIContent(element.FindPropertyRelative("Name").stringValue), true);
                },
            };

            views[player] = view;
            byObject[view.Set] = view;
            return view;
        }

        private const float FoldoutMargin = 10f;
        private const float ElementPadding = 2f;

        private static void DrawList(View view)
        {
            SerializedProperty animations = view.List.serializedProperty;

            if (animations.isExpanded)
            {
                view.List.DoLayoutList();
                return;
            }

            // Collapsed: the header alone, as Unity draws a collapsed list.
            Rect rect = GUILayoutUtility.GetRect(0f, view.List.headerHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) ReorderableList.defaultBehaviours.headerBackground.Draw(rect, false, false, false, false);

            rect.xMin += 6f;
            rect.xMax -= 6f;
            rect.height -= 2f;
            rect.y += 1f;

            DrawHeader(rect, animations, view.Asset);
            EditorGUILayout.Space(2f);
        }

        private static void DrawHeader(Rect rect, SerializedProperty animations, UIAnimationAsset asset)
        {
            var count = new GUIContent(animations.arraySize.ToString());
            float countWidth = EditorStyles.miniLabel.CalcSize(count).x;

            Rect foldout = new Rect(rect.x + FoldoutMargin, rect.y, rect.width - FoldoutMargin - countWidth - 4f, rect.height);

            animations.isExpanded = EditorGUI.Foldout(foldout, animations.isExpanded,
                new GUIContent("Shared Animations (" + asset.name + ")", Tooltip), true);

            GUI.Label(new Rect(rect.xMax - countWidth, rect.y, countWidth, rect.height), count, EditorStyles.miniLabel);
        }

        /// <summary>
        /// This player's On Complete entries whose animation is no longer in the set - deleted from it, or
        /// the set swapped for another. They can never run, so they are named here rather than left
        /// invisible, with a button to clear them out.
        /// </summary>
        private static void DrawOrphans(UIAnimationPlayer player, SerializedObject serialized, UIAnimationAsset asset)
        {
            SerializedProperty entries = serialized.FindProperty("SharedOnComplete");
            if (entries == null || entries.arraySize == 0) return;

            var orphans = new List<int>();
            var names = new List<string>();

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (IndexOfId(asset, entry.FindPropertyRelative("AnimationId").stringValue) >= 0) continue;

                orphans.Add(i);
                names.Add("'" + entry.FindPropertyRelative("AnimationName").stringValue + "'");
            }

            if (orphans.Count == 0) return;

            EditorGUILayout.HelpBox(
                "This player has its own On Complete for " + (orphans.Count == 1 ? "an animation" : orphans.Count + " animations") +
                " no longer in " + (asset != null ? "'" + asset.name + "'" : "a Shared Anim. Asset") + ": " +
                string.Join(", ", names) + ". " + (orphans.Count == 1 ? "It never runs." : "They never run."),
                MessageType.Info);

            Rect button = GUILayoutUtility.GetRect(new GUIContent("Remove"), EditorStyles.miniButton);
            button.xMin = button.xMax - 80f;

            if (GUI.Button(button, new GUIContent("Remove", "Deletes these On Complete entries from this player."),
                    EditorStyles.miniButton))
            {
                for (int i = orphans.Count - 1; i >= 0; i--)
                {
                    entries.DeleteArrayElementAtIndex(orphans[i]);
                }

                serialized.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }
        }

        private static void Prune()
        {
            var dead = new List<UIAnimationPlayer>();

            foreach (KeyValuePair<UIAnimationPlayer, View> pair in views)
            {
                if (pair.Key == null || pair.Value.Asset == null || pair.Value.Copy == null) dead.Add(pair.Key);
            }

            for (int i = 0; i < dead.Count; i++)
            {
                Forget(dead[i], views[dead[i]]);
            }
        }

        private static void Forget(UIAnimationPlayer player, View view)
        {
            views.Remove(player);
            byObject.Remove(view.Set);
            view.Set.Dispose();

            if (view.Copy != null) Object.DestroyImmediate(view.Copy);
        }

        // ---------------------------------------------------------------- this player's parts

        /// <summary>This player's On Complete entry for the set's animation with that Id, or null.</summary>
        public static SerializedProperty EntryFor(SerializedObject serialized, string id)
        {
            int index = EntryIndex(serialized, id);
            return index >= 0 ? serialized.FindProperty("SharedOnComplete").GetArrayElementAtIndex(index) : null;
        }

        private static int EntryIndex(SerializedObject serialized, string id)
        {
            if (serialized == null || string.IsNullOrEmpty(id)) return -1;

            SerializedProperty entries = serialized.FindProperty("SharedOnComplete");
            if (entries == null) return -1;

            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("AnimationId").stringValue == id) return i;
            }

            return -1;
        }

        private static int IndexOfId(UIAnimationAsset asset, string id)
        {
            if (asset == null || string.IsNullOrEmpty(id)) return -1;

            for (int i = 0; i < asset.Animations.Count; i++)
            {
                if (asset.Animations[i] != null && asset.Animations[i].Id == id) return i;
            }

            return -1;
        }

        /// <summary>
        /// Gives this player an On Complete for the set's animation at index, with one empty listener
        /// ready to fill in. The animation gets its Id first if it has none yet, and the set is saved
        /// straight away: a scene saved with an entry pointing at an Id the set never saved would lose it.
        /// One Undo step for both.
        /// </summary>
        public static void AddOnComplete(UIAnimationPlayer player, SerializedObject serialized, int index)
        {
            UIAnimationAsset asset = player.EditorShared;
            if (asset == null || index < 0 || index >= asset.Animations.Count || asset.Animations[index] == null) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            UIAnimation source = asset.Animations[index];

            if (string.IsNullOrEmpty(source.Id))
            {
                var set = new SerializedObject(asset);
                set.FindProperty("Animations").GetArrayElementAtIndex(index).FindPropertyRelative("Id").stringValue =
                    System.Guid.NewGuid().ToString("N");
                set.ApplyModifiedProperties();
                set.Dispose();

                AssetDatabase.SaveAssetIfDirty(asset);
            }

            serialized.Update();

            SerializedProperty entries = serialized.FindProperty("SharedOnComplete");
            int at = entries.arraySize;
            entries.arraySize = at + 1;

            SerializedProperty entry = entries.GetArrayElementAtIndex(at);
            entry.FindPropertyRelative("AnimationId").stringValue = source.Id;
            entry.FindPropertyRelative("AnimationName").stringValue = source.Name;

            // Growing a list copies its last element, listeners and all.
            SerializedProperty calls = entry.FindPropertyRelative("OnComplete.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            calls.arraySize = 1;
            ClearCall(calls.GetArrayElementAtIndex(0));

            serialized.ApplyModifiedProperties();

            Undo.SetCurrentGroupName("Add On Complete");
            Undo.CollapseUndoOperations(group);
        }

        /// <summary>A fresh listener, as the UnityEvent's own + makes one: nothing picked, Runtime Only.</summary>
        private static void ClearCall(SerializedProperty call)
        {
            call.FindPropertyRelative("m_Target").objectReferenceValue = null;
            call.FindPropertyRelative("m_MethodName").stringValue = string.Empty;
            call.FindPropertyRelative("m_Mode").intValue = 1;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.RuntimeOnly;
        }

        /// <summary>
        /// Copies the set's animation at index to the end of this player's own Animations, where it wins
        /// over the set's and can be edited, expanded and ready. This player's own On Complete for it moves
        /// onto the copy, after the set's listeners, so nothing it did is lost. One Undo step.
        /// </summary>
        public static void Override(UIAnimationPlayer player, SerializedObject serialized, int index)
        {
            UIAnimationAsset asset = player.EditorShared;
            if (asset == null || index < 0 || index >= asset.Animations.Count || asset.Animations[index] == null) return;

            UIAnimation source = asset.Animations[index];

            // The clipboard's JSON round trip: an independent copy with the set's listeners intact. The Id
            // is the set's, and means nothing on the player.
            var copy = new UIAnimation();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), copy);
            copy.Id = string.Empty;

            serialized.Update();

            SerializedProperty list = serialized.FindProperty("Animations");
            int at = list.arraySize;
            list.arraySize = at + 1;

            SerializedProperty element = list.GetArrayElementAtIndex(at);
            element.boxedValue = copy;
            element.isExpanded = true;
            list.isExpanded = true;

            int entry = EntryIndex(serialized, source.Id);
            if (entry >= 0)
            {
                SerializedProperty entries = serialized.FindProperty("SharedOnComplete");
                AppendCalls(entries.GetArrayElementAtIndex(entry).FindPropertyRelative("OnComplete"),
                    element.FindPropertyRelative("OnComplete"));
                entries.DeleteArrayElementAtIndex(entry);
            }

            serialized.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Override Animation");
        }

        private static void AppendCalls(SerializedProperty from, SerializedProperty to)
        {
            SerializedProperty source = from.FindPropertyRelative("m_PersistentCalls.m_Calls");
            SerializedProperty target = to.FindPropertyRelative("m_PersistentCalls.m_Calls");

            for (int i = 0; i < source.arraySize; i++)
            {
                int at = target.arraySize;
                target.arraySize = at + 1;
                CopyValues(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(at));
            }
        }

        /// <summary>
        /// Copies every value under from onto the same place under to. A listener cannot go through
        /// boxedValue - Unity refuses PersistentCall as a built-in type (measured, 6000.3) - so it is
        /// copied value by value, which also carries any field a later Unity adds.
        /// </summary>
        private static void CopyValues(SerializedProperty from, SerializedProperty to)
        {
            string prefix = from.propertyPath + ".";
            SerializedProperty source = from.Copy();
            bool enter = true;

            // By path rather than GetEndProperty, whose end for the last element of a list is not reliably
            // where the walk leaves it (measured: it ran on past it).
            while (source.Next(enter) && source.propertyPath.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                enter = source.propertyType == SerializedPropertyType.Generic;
                if (enter) continue;

                SerializedProperty target = to.FindPropertyRelative(source.propertyPath.Substring(prefix.Length));
                if (target == null || target.propertyType != source.propertyType) continue;

                switch (source.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.ArraySize:
                        target.longValue = source.longValue;
                        break;

                    case SerializedPropertyType.Enum:
                        target.intValue = source.intValue;
                        break;

                    case SerializedPropertyType.Boolean:
                        target.boolValue = source.boolValue;
                        break;

                    case SerializedPropertyType.Float:
                        target.doubleValue = source.doubleValue;
                        break;

                    case SerializedPropertyType.String:
                        target.stringValue = source.stringValue;
                        break;

                    case SerializedPropertyType.ObjectReference:
                        target.objectReferenceValue = source.objectReferenceValue;
                        break;
                }
            }
        }
    }
}
