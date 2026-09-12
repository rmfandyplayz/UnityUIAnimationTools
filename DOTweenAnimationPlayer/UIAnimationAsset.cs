// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{
    /// <summary>
    /// A set of named animations saved as a project asset, so many players can share one
    /// authored library instead of each holding its own copy.
    ///
    /// This is the OPT-IN path and it is not the default: authoring straight onto the player is
    /// fewer clicks and is what you want for anything only one object does. Reach for an asset
    /// when the same Show/Hide is on enough objects that retuning them by hand stops being
    /// reasonable - every panel in a menu, say.
    ///
    /// Assign it to a player's Shared slot. The player plays its own animations AND these, and a
    /// local animation of the same name wins, so one object can override a single animation out
    /// of the set without leaving it.
    ///
    /// A ScriptableObject cannot reference a scene object, so animations authored here cannot use
    /// the direct target slots - the inspector disables them and OnValidate below clears any that
    /// arrive by other means. That is less of a limitation than it sounds: an empty slot already
    /// means "the GameObject the player is on", and Target Path reaches a named child of it. Those
    /// two between them are what makes an animation portable enough to be worth sharing at all.
    /// </summary>
    [CreateAssetMenu(fileName = "UI Animation Set", menuName = "UI Animation/Animation Set", order = 200)]
    public class UIAnimationAsset : ScriptableObject
    {
        [Tooltip("The shared animations. Every player pointing at this asset can play all of them.")]
        public List<UIAnimation> Animations = new List<UIAnimation>();

        private void OnValidate()
        {
            for (int i = 0; i < Animations.Count; i++)
            {
                UIAnimation animation = Animations[i];
                if (animation == null) continue;

                animation.FillUnsetDefaults();
                ClearSceneTargets(animation);
            }

            WarnDuplicateNames();
        }

        /// <summary>
        /// Empties any direct target slot authored into this asset, and says so.
        ///
        /// Unity's object field will not usually let you drop a scene object onto an asset, but
        /// copy/paste will: a step copied off a player carries live instance IDs, so it pastes in
        /// here still pointing at the scene and then serializes to null at the next save with
        /// nothing said about it. Breaking it here, at the point of authoring, is the same call
        /// UIFlipbookClipAsset makes about a clip asset pointing at another clip asset.
        ///
        /// A prefab asset's components would survive serialization, which is worse rather than
        /// better - animating one would write to the prefab itself - so those go the same way.
        /// </summary>
        private void ClearSceneTargets(UIAnimation animation)
        {
            for (int s = 0; s < animation.Steps.Count; s++)
            {
                UIAnimationStep step = animation.Steps[s];
                if (step == null || !step.ClearDirectTargets()) continue;

                Debug.LogWarning(
                    "[UIAnimationAsset] " + name + ": animation '" + animation.Name + "' step " + s +
                    " had a target assigned directly. A shared asset cannot hold a scene reference, " +
                    "so the slot has been cleared. Use Target Path to reach a child, or leave it " +
                    "empty to use whichever GameObject the player is on.", this);
            }
        }

        /// <summary>
        /// Two animations of one name make the second unreachable, because a player resolves names
        /// through a dictionary where the first wins. Worth saying here rather than only from the
        /// player, since this is where the name was typed.
        /// </summary>
        private void WarnDuplicateNames()
        {
            for (int i = 0; i < Animations.Count; i++)
            {
                if (Animations[i] == null || string.IsNullOrEmpty(Animations[i].Name)) continue;

                for (int j = 0; j < i; j++)
                {
                    if (Animations[j] == null || Animations[j].Name != Animations[i].Name) continue;

                    Debug.LogWarning(
                        "[UIAnimationAsset] " + name + ": duplicate animation name '" + Animations[i].Name +
                        "'. The first one wins and the other cannot be played.", this);
                    break;
                }
            }
        }
    }
}
