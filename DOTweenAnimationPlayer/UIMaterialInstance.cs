// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
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

    #if UNITY_EDITOR
        // What the Graphic's material fields held before the instance went in, for EditorRelease and for a
        // scene saved mid-preview. TMP keeps its instance in a serialized field of its own as well,
        // m_fontMaterial, beside m_sharedMaterial.
        private bool editorNoted;
        private Material editorOriginal;
        private Material editorOriginalFont;

        private static readonly System.Reflection.FieldInfo FontMaterialField = typeof(TMP_Text).GetField("m_fontMaterial",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    #endif

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

        #if UNITY_EDITOR
            // Noted for the preview, which puts it back. The serialized field, not the getter: an Image with
            // no material of its own reads back as the default UI material, and writing that back would give
            // it one.
            if (!Application.isPlaying)
            {
                var serialized = new UnityEditor.SerializedObject(graphic);
                editorOriginal = EditorRead(serialized, graphic is TMP_Text ? "m_sharedMaterial" : "m_Material");
                editorOriginalFont = graphic is TMP_Text ? EditorRead(serialized, "m_fontMaterial") : null;
                editorNoted = true;
            }
        #endif

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

    #if UNITY_EDITOR
        /// <summary>
        /// Gives the Graphic back the material it had before the instance, and forgets the instance - for the
        /// end of the edit-mode preview, the only thing that instances one out of play mode. There the
        /// instance is assigned to a scene object like any edit, and a scene saved after a material preview
        /// kept a copy of it inside the scene file, cut off from the material asset (measured). The next
        /// read of Material makes a new instance.
        /// </summary>
        public void EditorRelease()
        {
            if (!initialized) return;

            if (graphic != null && editorNoted)
            {
                var serialized = new UnityEditor.SerializedObject(graphic);
                var text = graphic as TMP_Text;

                EditorWrite(serialized, text != null ? "m_sharedMaterial" : "m_Material", editorOriginal);
                if (text != null) EditorWrite(serialized, "m_fontMaterial", editorOriginalFont);

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (ownsInstance && instance != null) DestroyImmediate(instance);

            instance = null;
            ownsInstance = false;
            editorOriginal = null;
            editorOriginalFont = null;
            editorNoted = false;
            initialized = false;
        }

        /// <summary>
        /// The material the Graphic had before the edit-mode preview gave it the instance - the asset it was
        /// copied from - or null when the preview has not. Editing this while the instance is on screen changes
        /// nothing that shows, which is easy to do by accident.
        /// </summary>
        public Material EditorSource
        {
            get { return initialized && editorNoted ? editorOriginal : null; }
        }

        /// <summary>
        /// For saving a scene mid-preview: true puts the Graphic's own material back in its fields for the save,
        /// keeping the instance alive; false puts the instance back. Through the plain setters and TMP's field
        /// itself, so the scene is not marked as changed again once it is saved.
        /// </summary>
        public void EditorShowSource(bool source)
        {
            if (!initialized || !editorNoted || graphic == null || instance == null) return;

            var text = graphic as TMP_Text;
            if (text == null)
            {
                graphic.material = source ? editorOriginal : instance;
                return;
            }

            Material shared = source ? editorOriginal : instance;
            if (shared != null) text.fontSharedMaterial = shared;
            if (FontMaterialField != null) FontMaterialField.SetValue(text, source ? editorOriginalFont : instance);
        }

        private static Material EditorRead(UnityEditor.SerializedObject serialized, string field)
        {
            UnityEditor.SerializedProperty property = serialized.FindProperty(field);
            return property != null ? property.objectReferenceValue as Material : null;
        }

        private static void EditorWrite(UnityEditor.SerializedObject serialized, string field, Material value)
        {
            UnityEditor.SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.objectReferenceValue = value;
        }
    #endif

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
                    UIAnimationLog.Warn("UIMaterialInstance on '" + name + "'", "ANIMATED SHADER PROPERTIES WON'T SHOW.",
                        "It is under a stencil Mask ('" + mask.name + "'), and UGUI caches a copy of the material " +
                        "for stencil rendering. Use a RectMask2D instead, or untick Maskable on this Graphic.",
                        this);
                    return;
                }

                current = current.parent;
            }
        }
    }
}
