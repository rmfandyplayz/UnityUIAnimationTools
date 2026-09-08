// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// Gives a UI Graphic its own material instance so shader properties can be animated
    /// without ever writing to the shared project asset.
    ///
    /// Add this to any Image / RawImage / TMP text whose material a UIAnimationPlayer should drive.
    /// MaterialFloat and MaterialColor steps target this component, not a Graphic or Material,
    /// so there is no inspector slot that could point a tween at a shared asset by accident.
    ///
    /// Note: MaterialPropertyBlock does not work with UGUI - CanvasRenderer ignores it - so a real
    /// material instance is the only option.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UIMaterialInstance : MonoBehaviour
    {
        private Graphic graphic;
        private Material instance;
        private bool ownsInstance;
        private bool initialized;

        /// <summary>The per-element material. Safe to write to.</summary>
        public Material Material
        {
            get
            {
                Initialize();
                return instance;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            // Only destroy what we created. TMP owns its fontMaterial.
            if (ownsInstance && instance != null) Destroy(instance);
            instance = null;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            graphic = GetComponent<Graphic>();
            if (graphic == null) return;

            // TextMeshProUGUI overrides materialForRendering to read m_sharedMaterial and ignores
            // Graphic.material entirely, so assigning a clone there would have no visible effect.
            // fontMaterial is TMP own instance, and TMP owns its lifetime - do not destroy it.
            TMP_Text text = graphic as TMP_Text;
            if (text != null)
            {
                instance = text.fontMaterial;
                ownsInstance = false;
            }
            else
            {
                Material source = graphic.material;
                if (source == null) return;

                instance = new Material(source);
                instance.name = source.name + " (Instance)";
                ownsInstance = true;
                graphic.material = instance;
            }

            WarnIfUnderStencilMask();
        }

        /// <summary>
        /// A stencil Mask makes UGUI take a one-time cached copy of this material and never re-sync it,
        /// so animated properties would never reach the screen. RectMask2D does not do this.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void WarnIfUnderStencilMask()
        {
            MaskableGraphic maskable = graphic as MaskableGraphic;
            if (maskable == null || !maskable.maskable) return;

            Transform current = transform.parent;
            while (current != null)
            {
                Mask mask = current.GetComponent<Mask>();
                if (mask != null && mask.enabled)
                {
                    Debug.LogWarning(
                        "UIMaterialInstance on '" + name + "' is under a stencil Mask ('" + mask.name + "'). " +
                        "UGUI caches a copy of the material for stencil rendering, so animated shader " +
                        "properties will not be visible. Use a RectMask2D instead, or untick Maskable on this Graphic.",
                        this);
                    return;
                }

                current = current.parent;
            }
        }
    }
}
