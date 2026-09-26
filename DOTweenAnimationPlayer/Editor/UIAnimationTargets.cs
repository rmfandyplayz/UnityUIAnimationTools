// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// What a step in the Inspector points at, resolved by playback's own rules - the slot, then
    /// Target Path, then the player's own object - but with none of its side effects: no warnings,
    /// nothing written to the step, and never UIMaterialInstance.Material, whose first read creates
    /// a material instance and assigns it to the Graphic. Shared by the step drawer's type check and
    /// Use Current button and by the Scene-view gizmos, which all run on every repaint.
    /// </summary>
    internal static class UIAnimationTargets
    {
        /// <summary>
        /// The player a serialized object's steps run on: the player itself, or the Preview On player
        /// a shared asset's inspector has - the only scene an asset's steps can be resolved against.
        /// Null when editing several objects at once, or an asset with no preview player.
        /// </summary>
        public static UIAnimationPlayer OwnerOf(SerializedObject serialized)
        {
            if (serialized == null || serialized.isEditingMultipleObjects) return null;

            var player = serialized.targetObject as UIAnimationPlayer;
            if (player != null) return player;

            var asset = serialized.targetObject as UIAnimationAsset;
            return asset != null ? UIAnimationAssetEditor.PreviewPlayerFor(asset) : null;
        }

        /// <summary>The animation list a serialized object's "Animations" property is.</summary>
        public static List<UIAnimation> AnimationsOf(SerializedObject serialized)
        {
            var player = serialized.targetObject as UIAnimationPlayer;
            if (player != null) return player.EditorAnimations;

            var asset = serialized.targetObject as UIAnimationAsset;
            return asset != null ? asset.Animations : null;
        }

        private const string StepMarker = ".Steps.Array.data[";

        /// <summary>Reads the two indices out of "Animations.Array.data[a].Steps.Array.data[s]".</summary>
        public static bool TryParseStepPath(string path, out int animation, out int step)
        {
            animation = -1;
            step = -1;

            const string head = "Animations.Array.data[";
            int cut = path.IndexOf(StepMarker, System.StringComparison.Ordinal);
            if (!path.StartsWith(head, System.StringComparison.Ordinal) || cut < 0) return false;

            string first = path.Substring(head.Length, cut - 1 - head.Length);
            string second = path.Substring(cut + StepMarker.Length).TrimEnd(']');

            return int.TryParse(first, out animation) && int.TryParse(second, out step);
        }

        /// <summary>The step's own target slot for a type - the one the Inspector shows.</summary>
        public static Object SlotOf(UIAnimationStep step, UIAnimationStepType type)
        {
            switch (UIAnimationStep.TargetKindOf(type))
            {
                case UIAnimationTargetKind.CanvasGroup: return step.CanvasGroupTarget;
                case UIAnimationTargetKind.Graphic: return step.GraphicTarget;
                case UIAnimationTargetKind.Material: return step.MaterialTarget;
                case UIAnimationTargetKind.GameObject: return step.ActiveTarget;
                case UIAnimationTargetKind.Audio: return step.AudioSourceTarget;
                case UIAnimationTargetKind.Property: return step.PropertyTarget;
                default: return step.RectTarget;
            }
        }

        /// <summary>The RectTransform a position, size, scale or rotation step drives, or null.</summary>
        public static RectTransform RectOf(UIAnimationStep step, GameObject owner)
        {
            string problem;
            return Resolve(step.Type, SlotOf(step, step.Type), step.TargetPath, owner, out problem) as RectTransform;
        }

        /// <summary>
        /// The object a step of this type drives - a component, or a GameObject for SetActive and for a
        /// Custom Property, whose component ProblemOf checks separately - and null with a reason when
        /// there is none. A filled slot is taken as it is: its field type
        /// already guarantees the component. owner may be null (an asset with no preview player), in
        /// which case only a filled slot resolves and there is no problem to report.
        /// </summary>
        public static Object Resolve(UIAnimationStepType type, Object slot, string targetPath, GameObject owner,
            out string problem)
        {
            problem = null;
            if (slot != null) return slot;
            if (owner == null) return null;

            GameObject host = UIAnimationStep.FindHost(owner, targetPath);
            if (host == null)
            {
                problem = "Target Path \"" + targetPath + "\" matches nothing under '" + owner.name + "'" +
                          (UIAnimationStep.TargetKindOf(type) == UIAnimationTargetKind.Audio
                              ? " - the sound plays on the shared source instead."
                              : " - this step is skipped.");
                return null;
            }

            System.Type needed;
            string what;

            switch (UIAnimationStep.TargetKindOf(type))
            {
                case UIAnimationTargetKind.GameObject:
                case UIAnimationTargetKind.Property:
                    return host;

                case UIAnimationTargetKind.Audio:
                    // Optional by design: no AudioSource means the shared one, not a broken step.
                    return host.GetComponent<AudioSource>();

                case UIAnimationTargetKind.CanvasGroup:
                    needed = typeof(CanvasGroup);
                    what = "Canvas Group";
                    break;

                case UIAnimationTargetKind.Graphic:
                    needed = typeof(Graphic);
                    what = "Graphic (Image, Raw Image, Text or TextMeshPro)";
                    break;

                case UIAnimationTargetKind.Material:
                    needed = typeof(UIMaterialInstance);
                    what = "UI Material Instance";
                    break;

                default:
                    needed = typeof(RectTransform);
                    what = "RectTransform - it is not a UI object";
                    break;
            }

            Component found = host.GetComponent(needed);
            if (found == null) problem = "'" + host.name + "' has no " + what + ", so this step does nothing.";

            return found;
        }

        /// <summary>
        /// Everything wrong with a step that the Inspector can see without playing it, or null. Checks
        /// the target (a missed Target Path, a missing component), a material step's shader property,
        /// and a sound step's clip - the same things playback warns about, said before Play rather than
        /// in the Console after it.
        /// </summary>
        public static string ProblemOf(SerializedProperty step, UIAnimationStepType type, UIAnimationPlayer owner)
        {
            if (type == UIAnimationStepType.PlaySound && step.FindPropertyRelative("Clip").objectReferenceValue == null)
            {
                return "No Clip - this step plays nothing.";
            }

            string slotField = SlotField(type);
            Object slot = step.FindPropertyRelative(slotField).objectReferenceValue;
            string path = step.FindPropertyRelative("TargetPath").stringValue;

            string problem;
            Object target = Resolve(type, slot, path, owner != null ? owner.gameObject : null, out problem);
            if (problem != null) return problem;

            if (UIAnimationStep.TargetKindOf(type) == UIAnimationTargetKind.Property) return PropertyProblemOf(step, target as GameObject);

            if (UIAnimationStep.TargetKindOf(type) != UIAnimationTargetKind.Material) return null;

            string property = step.FindPropertyRelative("ShaderProperty").stringValue;
            if (string.IsNullOrEmpty(property)) return "No Shader Property name - this step is skipped.";

            Material material = SourceMaterialOf(target as UIMaterialInstance);
            if (material != null && !material.HasProperty(property))
            {
                return "Shader '" + material.shader.name + "' has no property '" + property + "' - this step is skipped.";
            }

            return null;
        }

        /// <summary>
        /// What is wrong with a Custom Property step's pick on the object it resolved to: nothing picked,
        /// a component or member that is not there, or a member whose type has changed since. With no
        /// object to look on - an asset with no Preview On player - only the first can be checked.
        /// Looks members up without reading them, since a getter is arbitrary code and this runs on
        /// every repaint.
        /// </summary>
        public static string PropertyProblemOf(SerializedProperty step, GameObject host)
        {
            Component component;
            MemberInfo member;
            string problem;

            UIAnimationProperties.TryLocate(host,
                step.FindPropertyRelative("PropertyComponent").stringValue,
                step.FindPropertyRelative("PropertyMember").stringValue,
                (UIAnimationPropertyKind)step.FindPropertyRelative("PropertyKind").intValue,
                out component, out member, out problem);

            return problem;
        }

        /// <summary>
        /// The material a UIMaterialInstance will clone, read without creating the clone: TMP's font
        /// material, or the Graphic's own. Null when there is nothing to read.
        /// </summary>
        public static Material SourceMaterialOf(UIMaterialInstance instance)
        {
            if (instance == null) return null;

            var graphic = instance.GetComponent<Graphic>();
            if (graphic == null) return null;

            var text = graphic as TMP_Text;
            return text != null ? text.fontSharedMaterial : graphic.material;
        }

        /// <summary>The name of the target slot a step of this type shows.</summary>
        public static string SlotField(UIAnimationStepType type)
        {
            switch (UIAnimationStep.TargetKindOf(type))
            {
                case UIAnimationTargetKind.CanvasGroup: return "CanvasGroupTarget";
                case UIAnimationTargetKind.Graphic: return "GraphicTarget";
                case UIAnimationTargetKind.Material: return "MaterialTarget";
                case UIAnimationTargetKind.GameObject: return "ActiveTarget";
                case UIAnimationTargetKind.Audio: return "AudioSourceTarget";
                case UIAnimationTargetKind.Property: return "PropertyTarget";
                default: return "RectTarget";
            }
        }
    }
}
