// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// Save as Animation Set, on the right-click menu of a player's Animations list: copies every
    /// animation on the player into a UIAnimationAsset, ready for other players' Shared slots. The
    /// player itself is left as it was.
    ///
    /// An asset cannot hold a scene reference, so a filled target slot is turned into a Target Path
    /// from the player instead - `..` included, since a slot often points above the player - and
    /// checked by resolving it back the way playback will. Emptying the slot instead would not lose
    /// the step quietly: an empty slot means the player's own object, so the set would animate the
    /// wrong thing. A target the path cannot reach (under another root, or sharing its name with a
    /// sibling) gets its path from the scene root, which misses and warns when played, and is listed
    /// in the Console. On Complete listeners on scene objects are left out, as an asset would drop
    /// them anyway.
    ///
    /// Where it saves: "Save as Animation Set..." always asks, starting in the folder last saved to
    /// this session (SessionState), else Assets/ScriptableObjects, else Assets. Once a folder has
    /// been picked, a second entry saves straight there without asking. That is the "default folder"
    /// without a settings window: it lasts until the Editor closes, and the first entry changes it.
    /// </summary>
    internal static class UIAnimationSetExport
    {
        private const string FolderKey = "rmf_claude.DOTweenUI.AnimationSetFolder";
        private const string FallbackFolder = "Assets/ScriptableObjects";

        public static void AddMenuItems(GenericMenu menu, UIAnimationPlayer player, SerializedProperty list)
        {
            var ask = new GUIContent("Save as Animation Set...");

            menu.AddSeparator(string.Empty);

            if (list.arraySize == 0)
            {
                menu.AddDisabledItem(ask);
                return;
            }

            menu.AddItem(ask, false, () => SaveAsking(player, list));

            string remembered = RememberedFolder();
            if (remembered == null) return;

            // The folder's own name only: a slash in a menu item makes a submenu.
            menu.AddItem(new GUIContent("Save as Animation Set in '" + Path.GetFileName(remembered) + "'"), false,
                () => Save(player, list, AssetDatabase.GenerateUniqueAssetPath(remembered + "/" + DefaultName(player) + ".asset")));
        }

        // ---------------------------------------------------------------- saving

        private static void SaveAsking(UIAnimationPlayer player, SerializedProperty list)
        {
            string start = RememberedFolder() ?? (AssetDatabase.IsValidFolder(FallbackFolder) ? FallbackFolder : "Assets");

            string path = EditorUtility.SaveFilePanelInProject("Save as Animation Set", DefaultName(player), "asset",
                "Where to save the animation set made from '" + player.name + "'.", start);

            if (string.IsNullOrEmpty(path)) return;

            SessionState.SetString(FolderKey, Path.GetDirectoryName(path).Replace('\\', '/'));
            Save(player, list, path);
        }

        private static void Save(UIAnimationPlayer player, SerializedProperty list, string path)
        {
            if (player == null) return;

            var report = new Report();
            List<UIAnimation> animations = Build(player, list, report);

            // Saving over an existing set rewrites it in place. CreateAsset would delete it first and
            // give the new one a new GUID, and every Shared slot pointing at the old one would go empty.
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            UIAnimationAsset asset = existing as UIAnimationAsset;

            if (existing != null && asset == null)
            {
                EditorUtility.DisplayDialog("Save as Animation Set",
                    path + " is not an animation set, so it was not replaced. Pick another name.", "OK");
                return;
            }

            if (asset != null)
            {
                KeepIds(asset, animations);

                Undo.RecordObject(asset, "Save as Animation Set");
                asset.Animations = animations;
                EditorUtility.SetDirty(asset);
            }
            else
            {
                asset = ScriptableObject.CreateInstance<UIAnimationAsset>();
                asset.Animations = animations;
                AssetDatabase.CreateAsset(asset, path);
            }

            // Only this asset: SaveAssets would flush every dirty asset in the project.
            AssetDatabase.SaveAssetIfDirty(asset);
            EditorGUIUtility.PingObject(asset);

            report.Log(player, asset, path, animations.Count, existing != null);
        }

        // ---------------------------------------------------------------- building

        private static List<UIAnimation> Build(UIAnimationPlayer player, SerializedProperty list, Report report)
        {
            list.serializedObject.Update();

            var animations = new List<UIAnimation>(list.arraySize);

            for (int a = 0; a < list.arraySize; a++)
            {
                // The same JSON round trip the clipboard uses: an independent copy, references intact
                // for the conversion below to read.
                var animation = new UIAnimation();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(list.GetArrayElementAtIndex(a).boxedValue), animation);

                for (int s = 0; s < animation.Steps.Count; s++)
                {
                    UIAnimationStep step = animation.Steps[s];
                    if (step == null) continue;

                    ConvertSlot(player.gameObject, animation, s, step, report);

                    // The other types' slots too: invisible leftovers, which the asset would clear anyway.
                    step.ClearDirectTargets();
                }

                // An Id only means something inside a set; see KeepIds.
                animation.Id = string.Empty;

                DropSceneListeners(animation, report);
                animations.Add(animation);
            }

            return animations;
        }

        private static void ConvertSlot(GameObject owner, UIAnimation animation, int index, UIAnimationStep step, Report report)
        {
            Object slot = UIAnimationTargets.SlotOf(step, step.Type);
            if (slot == null) return;

            GameObject target = slot as GameObject;
            if (target == null && slot is Component) target = ((Component)slot).gameObject;
            if (target == null) return;

            string path = RelativePath(owner.transform, target.transform);

            string problem;
            if (path != null && UIAnimationTargets.Resolve(step.Type, null, path, owner, out problem) == slot)
            {
                step.TargetPath = path;
                report.Converted++;
                return;
            }

            step.TargetPath = RootPath(target.transform);
            report.Unreachable.Add("'" + animation.Name + "' step " + index + " (" + step.Type + " on '" + target.name +
                                   "'): Target Path set to \"" + step.TargetPath + "\"");
        }

        /// <summary>
        /// The Target Path from owner to target: up with `..` to the nearest shared ancestor, then down
        /// by name. Empty when they are the same object, null when they share no ancestor.
        /// </summary>
        private static string RelativePath(Transform owner, Transform target)
        {
            var above = new List<Transform>();
            for (Transform t = owner; t != null; t = t.parent) above.Add(t);

            var down = new List<string>();
            Transform meet = target;

            while (meet != null && !above.Contains(meet))
            {
                down.Add(meet.name);
                meet = meet.parent;
            }

            if (meet == null) return null;

            var parts = new List<string>();
            for (int i = above.IndexOf(meet); i > 0; i--) parts.Add("..");
            for (int i = down.Count - 1; i >= 0; i--) parts.Add(down[i]);

            return string.Join("/", parts);
        }

        private static string RootPath(Transform target)
        {
            var names = new List<string>();
            for (Transform t = target; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>
        /// Leaves out every On Complete listener aimed at a GameObject or a component. An asset would
        /// save a scene one as missing, and a prefab one would call into the prefab asset itself.
        /// Listeners on other assets, a ScriptableObject say, are kept.
        /// </summary>
        private static void DropSceneListeners(UIAnimation animation, Report report)
        {
            if (animation.OnComplete == null) return;

            for (int i = animation.OnComplete.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                Object target = animation.OnComplete.GetPersistentTarget(i);
                if (!(target is GameObject) && !(target is Component)) continue;

                UnityEventTools.RemovePersistentListener(animation.OnComplete, i);
                report.DroppedListeners++;
            }
        }

        /// <summary>
        /// Saving over a set gives each new animation the Id of the set's animation of the same name, so a
        /// player's own On Complete for it (found by Id) survives the set being saved again.
        /// </summary>
        private static void KeepIds(UIAnimationAsset asset, List<UIAnimation> animations)
        {
            for (int i = 0; i < animations.Count; i++)
            {
                UIAnimation old = asset.Animations.Find(a => a != null && a.Name == animations[i].Name);
                if (old != null) animations[i].Id = old.Id;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static string RememberedFolder()
        {
            string folder = SessionState.GetString(FolderKey, string.Empty);
            return AssetDatabase.IsValidFolder(folder) ? folder : null;
        }

        private static string DefaultName(UIAnimationPlayer player)
        {
            string name = player.name + " Animation Set";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private class Report
        {
            public int Converted;
            public int DroppedListeners;
            public readonly List<string> Unreachable = new List<string>();

            public void Log(UIAnimationPlayer player, UIAnimationAsset asset, string path, int count, bool replaced)
            {
                var text = new StringBuilder();
                text.Append("Saved ").Append(count).Append(count == 1 ? " animation" : " animations")
                    .Append(" from '").Append(player.name).Append("' to ").Append(path)
                    .Append(replaced ? ", replacing what was there." : ".");

                if (Converted > 0)
                {
                    text.Append(' ').Append(Converted).Append(Converted == 1 ? " target slot" : " target slots")
                        .Append(" became a Target Path from '").Append(player.name).Append("'.");
                }

                if (DroppedListeners > 0)
                {
                    text.Append(' ').Append(DroppedListeners)
                        .Append(DroppedListeners == 1 ? " On Complete listener" : " On Complete listeners")
                        .Append(" on scene objects left out: a set cannot hold them.");
                }

                if (Unreachable.Count > 0)
                {
                    text.Append("\nThese targets can't be reached from '").Append(player.name)
                        .Append("' by name (under another root, or sharing a name with a sibling), so they will warn and do nothing until fixed:");

                    for (int i = 0; i < Unreachable.Count; i++) text.Append("\n  ").Append(Unreachable[i]);
                }

                text.Append("\n'").Append(player.name).Append("' still has its own copies, and they win over the set's.");

                if (Unreachable.Count == 0)
                {
                    Debug.Log(text.ToString(), asset);
                    return;
                }

                UIAnimationLog.Warn("Save as Animation Set",
                    Unreachable.Count == 1 ? "ONE STEP WON'T FIND ITS TARGET." : Unreachable.Count + " STEPS WON'T FIND THEIR TARGETS.",
                    text.ToString(), asset);
            }
        }
    }
}
