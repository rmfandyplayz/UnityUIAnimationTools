// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Right-click authoring commands for animations and steps: copy, paste, and mirror.
    ///
    /// Unity's own generic Copy/Paste on a serialized property drops object references, which
    /// for this data means every target slot comes back empty. JsonUtility keeps them, so a
    /// pasted step still points at the Graphic or AudioClip it was copied from.
    ///
    /// It must be JsonUtility and NOT EditorJsonUtility, despite the name suggesting otherwise.
    /// EditorJsonUtility serialises the way an asset file does, where a reference is a file ID -
    /// an in-memory scene object has none, so every reference is written as {"instanceID":0} and
    /// silently lost. JsonUtility writes the live instance ID, which resolves for the lifetime of
    /// the Editor session. Measured both ways; this regressed once and dropped every target slot.
    ///
    /// Entries appear on the property context menu:
    ///   right-click an animation header  -> Copy / Paste / Mirror / Duplicate as Mirrored
    ///   right-click a step header        -> Copy / Paste / Mirror
    ///   right-click the Animations list  -> Paste Animation (add to end / mirrored)
    ///   right-click the Steps list       -> Paste Step (add to end / mirrored)
    ///
    /// On a UIAnimationPlayer and on a UIAnimationAsset alike, so an animation can be copied out
    /// of a player and into a shared set or back. Pasting INTO an asset carries the source's
    /// target references with it, and UIAnimationAsset.OnValidate clears them - a scene reference
    /// is exactly what an asset cannot keep.
    ///
    /// One registration for the whole folder. Two handlers on contextualPropertyMenu would
    /// append to the same menu in whatever order their static constructors happened to run.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIAnimationContextMenu
    {
        private const string AnimationTypeName = "UIAnimation";
        private const string StepTypeName = "UIAnimationStep";
        private const string ElementSuffix = ".Array.data[";

        // Held as JSON rather than as a live object, so editing the source afterwards
        // does not retroactively change what is on the clipboard.
        private static string animationJson;
        private static string stepJson;

        static UIAnimationContextMenu()
        {
            EditorApplication.contextualPropertyMenu -= OnContextMenu;
            EditorApplication.contextualPropertyMenu += OnContextMenu;
        }

        private static void OnContextMenu(GenericMenu menu, SerializedProperty property)
        {
            // Guards out strings, which also report isArray == true.
            if (property.propertyType != SerializedPropertyType.Generic) return;

            // SerializedProperty.type reports the UNQUALIFIED type name, so a namespace does not
            // disambiguate it and any other project's "UIAnimation" would match the strings below.
            // Requiring one of our own objects as the inspected one is what actually scopes this.
            Object inspected = property.serializedObject.targetObject;
            if (!(inspected is UIAnimationPlayer) && !(inspected is UIAnimationAsset)) return;

            // The property handed to this callback is only valid for the duration of the call,
            // and menu items run later. Copy it so the deferred handler has something to use.
            SerializedProperty captured = property.Copy();

            if (property.isArray)
            {
                if (property.arrayElementType == AnimationTypeName) AddListEntries(menu, captured, true);
                else if (property.arrayElementType == StepTypeName) AddListEntries(menu, captured, false);
                return;
            }

            if (property.type == AnimationTypeName) AddElementEntries(menu, captured, true);
            else if (property.type == StepTypeName) AddElementEntries(menu, captured, false);
        }

        // ---------------------------------------------------------------- menu building

        private static void AddElementEntries(GenericMenu menu, SerializedProperty element, bool isAnimation)
        {
            string noun = isAnimation ? "Animation" : "Step";

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy " + noun), false, () => Copy(element, isAnimation));

            var paste = new GUIContent("Paste " + noun + " (overwrite)");

            if (HasCopy(isAnimation)) menu.AddItem(paste, false, () => Paste(element, isAnimation));
            else menu.AddDisabledItem(paste);

            // Inserting rather than overwriting. Without these the only paste that does not destroy
            // something is "add to end", and landing a step in the middle means adding a blank one
            // and dragging it up the list by hand.
            var above = new GUIContent("Paste " + noun + " Above");
            var below = new GUIContent("Paste " + noun + " Below");

            if (HasCopy(isAnimation) && ParentList(element) != null)
            {
                menu.AddItem(above, false, () => Insert(element, isAnimation, false));
                menu.AddItem(below, false, () => Insert(element, isAnimation, true));
            }
            else
            {
                menu.AddDisabledItem(above);
                menu.AddDisabledItem(below);
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Mirror " + noun), false, () => MirrorInPlace(element, isAnimation));

            if (!isAnimation) return;

            // The most useful one: author Show, then get a real editable Hide in a single click.
            var duplicate = new GUIContent("Duplicate as Mirrored");

            if (ParentList(element) != null) menu.AddItem(duplicate, false, () => DuplicateMirrored(element));
            else menu.AddDisabledItem(duplicate);
        }

        private static void AddListEntries(GenericMenu menu, SerializedProperty list, bool isAnimation)
        {
            string noun = isAnimation ? "Animation" : "Step";

            var paste = new GUIContent("Paste " + noun + " (add to end)");
            var pasteMirrored = new GUIContent("Paste " + noun + " Mirrored (add to end)");

            menu.AddSeparator(string.Empty);

            if (HasCopy(isAnimation))
            {
                menu.AddItem(paste, false, () => Append(list, isAnimation, false));
                menu.AddItem(pasteMirrored, false, () => Append(list, isAnimation, true));
            }
            else
            {
                menu.AddDisabledItem(paste);
                menu.AddDisabledItem(pasteMirrored);
            }
        }

        // ---------------------------------------------------------------- commands

        private static bool HasCopy(bool isAnimation)
        {
            return !string.IsNullOrEmpty(isAnimation ? animationJson : stepJson);
        }

        private static void Copy(SerializedProperty element, bool isAnimation)
        {
            string json = JsonUtility.ToJson(element.boxedValue);

            if (isAnimation) animationJson = json;
            else stepJson = json;
        }

        private static void Paste(SerializedProperty element, bool isAnimation)
        {
            object value = Rebuild(isAnimation);
            if (value == null) return;

            element.boxedValue = value;
            element.serializedObject.ApplyModifiedProperties();
        }

        private static void MirrorInPlace(SerializedProperty element, bool isAnimation)
        {
            // Round-trip through JSON rather than mutating whatever boxedValue hands back, so a
            // live reference could never leave the source half-mirrored if a step threw.
            object value = Clone(element, isAnimation);
            if (value == null) return;

            if (isAnimation) UIAnimationMirror.Mirror((UIAnimation)value, element.serializedObject.targetObject);
            else UIAnimationMirror.MirrorStep((UIAnimationStep)value);

            element.boxedValue = value;
            element.serializedObject.ApplyModifiedProperties();
        }

        private static void DuplicateMirrored(SerializedProperty element)
        {
            SerializedProperty list = ParentList(element);
            if (list == null) return;

            var animation = (UIAnimation)Clone(element, true);
            if (animation == null) return;

            UIAnimationMirror.Mirror(animation, element.serializedObject.targetObject);
            animation.Name = animation.Name + " Mirrored";

            AddToEnd(list, animation, true);
        }

        private static void Append(SerializedProperty list, bool isAnimation, bool mirrored)
        {
            object value = Rebuild(isAnimation);
            if (value == null) return;

            if (mirrored)
            {
                if (isAnimation) UIAnimationMirror.Mirror((UIAnimation)value, list.serializedObject.targetObject);
                else UIAnimationMirror.MirrorStep((UIAnimationStep)value);
            }

            AddToEnd(list, value, isAnimation);
        }

        /// <summary>
        /// Pastes the clipboard as a NEW element next to the one right-clicked, shuffling the rest
        /// down, rather than overwriting it.
        ///
        /// The pasted Start mode is left exactly as copied. Pasting a With Previous step is
        /// therefore how you deliberately widen a joined group, and the framework already treats
        /// whatever ends up at index 0 as opening a group regardless of what its Start says.
        /// </summary>
        private static void Insert(SerializedProperty element, bool isAnimation, bool below)
        {
            SerializedProperty list = ParentList(element);
            if (list == null) return;

            object value = Rebuild(isAnimation);
            if (value == null) return;

            int index = ElementIndex(element);
            if (index < 0) return;

            if (below) index++;

            // InsertArrayElementAtIndex duplicates the neighbouring element, so the slot is
            // overwritten immediately afterwards rather than left as an accidental copy.
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).boxedValue = value;

            if (isAnimation) MakeNameUnique(list, index);

            list.serializedObject.ApplyModifiedProperties();
        }

        private static void AddToEnd(SerializedProperty list, object value, bool isAnimation)
        {
            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).boxedValue = value;

            // Play() resolves names through a dictionary where the first duplicate wins, so an
            // added animation keeping an existing name would be silently unreachable.
            if (isAnimation) MakeNameUnique(list, index);

            list.serializedObject.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// The list a list element belongs to, found by trimming ".Array.data[n]" off its path.
        /// Null when the property is not a list element.
        /// </summary>
        private static SerializedProperty ParentList(SerializedProperty element)
        {
            int cut = element.propertyPath.LastIndexOf(ElementSuffix);
            if (cut < 0) return null;

            return element.serializedObject.FindProperty(element.propertyPath.Substring(0, cut));
        }

        /// <summary>
        /// A list element's own index, read back out of the "...Array.data[n]" tail of its path.
        /// -1 when the property is not a list element.
        /// </summary>
        private static int ElementIndex(SerializedProperty element)
        {
            string path = element.propertyPath;

            int open = path.LastIndexOf(ElementSuffix);
            if (open < 0) return -1;

            open += ElementSuffix.Length;
            int close = path.IndexOf(']', open);
            if (close < 0) return -1;

            int index;
            return int.TryParse(path.Substring(open, close - open), out index) ? index : -1;
        }

        /// <summary>
        /// An independent copy of a property's value, references intact. boxedValue already hands
        /// back a deep copy, but round-tripping keeps one mechanism for cloning and clipboard
        /// alike, so a reference bug can only ever exist in one place.
        /// </summary>
        private static object Clone(SerializedProperty element, bool isAnimation)
        {
            return FromJson(JsonUtility.ToJson(element.boxedValue), isAnimation);
        }

        /// <summary>Deserializes the clipboard onto a fresh instance.</summary>
        private static object Rebuild(bool isAnimation)
        {
            if (!HasCopy(isAnimation)) return null;
            return FromJson(isAnimation ? animationJson : stepJson, isAnimation);
        }

        /// <summary>
        /// Starting from `new` rather than an empty object means any field the JSON happens
        /// not to carry keeps its C# default instead of arriving zeroed.
        /// </summary>
        private static object FromJson(string json, bool isAnimation)
        {
            if (string.IsNullOrEmpty(json)) return null;

            if (isAnimation)
            {
                var animation = new UIAnimation();
                JsonUtility.FromJsonOverwrite(json, animation);
                return animation;
            }

            var step = new UIAnimationStep();
            JsonUtility.FromJsonOverwrite(json, step);
            return step;
        }

        private static void MakeNameUnique(SerializedProperty list, int index)
        {
            SerializedProperty nameProperty = list.GetArrayElementAtIndex(index).FindPropertyRelative("Name");
            if (nameProperty == null || string.IsNullOrEmpty(nameProperty.stringValue)) return;

            string baseName = nameProperty.stringValue;
            string candidate = baseName;
            int suffix = 1;

            while (NameTaken(list, index, candidate))
            {
                suffix++;
                candidate = baseName + " " + suffix;
            }

            nameProperty.stringValue = candidate;
        }

        private static bool NameTaken(SerializedProperty list, int skipIndex, string candidate)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (i == skipIndex) continue;

                SerializedProperty other = list.GetArrayElementAtIndex(i).FindPropertyRelative("Name");
                if (other != null && other.stringValue == candidate) return true;
            }

            return false;
        }
    }
}
