// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Right-click commands on a clip's frame list: sort, reverse, clear.
    ///
    /// Getting a dozen sprites into the right order is the actual cost of authoring a flipbook.
    /// Unity's multi-drag order is not dependable and plain string sorting puts frame_10 before
    /// frame_2, so the sort here compares runs of digits as numbers.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIFlipbookContextMenu
    {
        static UIFlipbookContextMenu()
        {
            // Registered once for this folder. Every handler on this event appends to the same menu,
            // in whatever order the static constructors happened to run, so a second registration
            // here would silently duplicate every item below.
            EditorApplication.contextualPropertyMenu -= OnContextMenu;
            EditorApplication.contextualPropertyMenu += OnContextMenu;
        }

        private static void OnContextMenu(GenericMenu menu, SerializedProperty property)
        {
            // SerializedProperty.type and propertyPath report UNQUALIFIED names, so a namespace does
            // not disambiguate them and any other project's "Frames" list would match. Requiring one
            // of our own objects as the inspected target is what actually scopes this.
            var owner = property.serializedObject.targetObject;

            if (!(owner is UISpriteFlipbook || owner is UIFlipbookSelectable || owner is UIFlipbookClipAsset))
            {
                return;
            }

            var frames = FramesFor(property);
            if (frames == null) return;

            // The property handed to this callback goes out of scope as soon as it returns, but the
            // menu items run later.
            var copy = frames.Copy();
            var count = copy.arraySize;

            Separate(menu);

            if (count > 1)
            {
                menu.AddItem(new GUIContent("Sort Frames By Name"), false, () => SortByName(copy));
                menu.AddItem(new GUIContent("Reverse Frames"), false, () => Reverse(copy));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Sort Frames By Name"));
                menu.AddDisabledItem(new GUIContent("Reverse Frames"));
            }

            if (count > 0) menu.AddItem(new GUIContent("Clear Frames"), false, () => Clear(copy));
            else menu.AddDisabledItem(new GUIContent("Clear Frames"));
        }

        /// <summary>
        /// A divider before these entries, unless the menu already ends in one. Unity 6000.3 ends its
        /// own part of every property menu with a divider before any handler runs (measured), so adding
        /// one regardless drew two in a row on Windows. GenericMenu has no public way to read its items,
        /// so this looks at its private list, and adds the divider anyway if that stops being readable.
        /// </summary>
        private static void Separate(GenericMenu menu)
        {
            if (menu.GetItemCount() == 0 || EndsWithSeparator(menu)) return;
            menu.AddSeparator("");
        }

        private static FieldInfo menuItemsField;

        private static bool EndsWithSeparator(GenericMenu menu)
        {
            if (menuItemsField == null)
            {
                menuItemsField = typeof(GenericMenu).GetField("m_MenuItems", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var items = menuItemsField != null ? menuItemsField.GetValue(menu) as IList : null;
            if (items == null || items.Count == 0) return false;

            var last = items[items.Count - 1];
            var separator = last != null ? last.GetType().GetField("separator") : null;

            return separator != null && separator.FieldType == typeof(bool) && (bool)separator.GetValue(last);
        }

        /// <summary>
        /// The Frames list the click was aimed at: the list itself, the clip that owns one, or an
        /// individual frame inside one - all three are places you would reasonably right-click.
        /// </summary>
        private static SerializedProperty FramesFor(SerializedProperty property)
        {
            var path = property.propertyPath;

            if (property.isArray && LastSegment(path) == "Frames") return property;

            if (property.type == "UIFlipbookClip")
            {
                return property.FindPropertyRelative("Frames");
            }

            // An element: "Normal.Frames.Array.data[3]" -> "Normal.Frames".
            var marker = path.IndexOf(".Array.data[", System.StringComparison.Ordinal);

            if (marker > 0)
            {
                var listPath = path.Substring(0, marker);

                if (LastSegment(listPath) == "Frames")
                {
                    return property.serializedObject.FindProperty(listPath);
                }
            }

            return null;
        }

        private static string LastSegment(string path)
        {
            var dot = path.LastIndexOf('.');
            return dot < 0 ? path : path.Substring(dot + 1);
        }

        // ------------------------------------------------------------------------------- commands

        private static void SortByName(SerializedProperty frames)
        {
            var items = Read(frames);
            items.Sort(CompareNatural);
            Write(frames, items);
        }

        private static void Reverse(SerializedProperty frames)
        {
            var items = Read(frames);
            items.Reverse();
            Write(frames, items);
        }

        private static void Clear(SerializedProperty frames)
        {
            frames.ClearArray();
            frames.serializedObject.ApplyModifiedProperties();
        }

        private static List<Object> Read(SerializedProperty frames)
        {
            var items = new List<Object>(frames.arraySize);

            for (int i = 0; i < frames.arraySize; i++)
            {
                items.Add(frames.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            return items;
        }

        private static void Write(SerializedProperty frames, List<Object> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }

            // ApplyModifiedProperties registers the Undo entry for us.
            frames.serializedObject.ApplyModifiedProperties();
        }

        // -------------------------------------------------------------------------- natural sort

        private static int CompareNatural(Object a, Object b)
        {
            // An empty slot is a deliberate blank beat in the drawing, but it has no name to sort
            // by, so those go to the end rather than clumping at the front.
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            return CompareNatural(a.name, b.name);
        }

        /// <summary>
        /// Case-insensitive compare that treats a run of digits as one number, so frame_2 sorts
        /// before frame_10 the way a human reads them.
        /// </summary>
        private static int CompareNatural(string x, string y)
        {
            int i = 0, j = 0;

            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    int startX = i, startY = j;

                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;

                    // Leading zeros are padding, not value: frame_007 and frame_7 are the same number.
                    var numberX = x.Substring(startX, i - startX).TrimStart('0');
                    var numberY = y.Substring(startY, j - startY).TrimStart('0');

                    if (numberX.Length != numberY.Length) return numberX.Length - numberY.Length;

                    var digits = string.CompareOrdinal(numberX, numberY);
                    if (digits != 0) return digits;

                    continue;
                }

                var charX = char.ToLowerInvariant(x[i]);
                var charY = char.ToLowerInvariant(y[j]);

                if (charX != charY) return charX - charY;

                i++;
                j++;
            }

            return (x.Length - i) - (y.Length - j);
        }
    }
}
