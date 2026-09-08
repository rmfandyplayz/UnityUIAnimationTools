// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Runs one flipbook in edit mode, off the Editor's own update loop.
    ///
    /// A preview writes Image.sprite on a REAL scene object, so the whole thing is bracketed:
    /// the sprite is captured before anything moves and written back when the preview stops, and
    /// an Undo entry is recorded so Ctrl+Z is a backstop if something goes wrong anyway. Only one
    /// preview runs at a time - starting a second stops the first, which is what guarantees the
    /// captured sprite always belongs to the object being restored.
    ///
    /// Completion callbacks are suppressed for the duration, because an OnCompleted UnityEvent is
    /// wired to arbitrary game code and a preview must not run that outside play mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIFlipbookPreview
    {
        // An editor frame can be arbitrarily long (a compile, a dragged window, a modal dialog).
        // Clamping stops the flipbook fast-forwarding through a pile of frames afterwards.
        private const float MaxDelta = 0.1f;

        private static UISpriteFlipbook target;
        private static Image capturedImage;
        private static Sprite capturedSprite;
        private static double lastTime;

        static UIFlipbookPreview()
        {
            // Static state does not survive either of these, so the sprite would be stranded on
            // whatever frame the preview happened to be showing.
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static bool IsActive
        {
            get { return target != null; }
        }

        internal static UISpriteFlipbook Target
        {
            get { return target; }
        }

        internal static bool IsPreviewing(UISpriteFlipbook flipbook)
        {
            return target != null && target == flipbook;
        }

        /// <summary>Start previewing <paramref name="clip"/> on <paramref name="flipbook"/>.</summary>
        internal static void Begin(UISpriteFlipbook flipbook, UIFlipbookClip clip)
        {
            End();

            if (flipbook == null || clip == null) return;

            var image = flipbook.EditorImage;

            if (image == null)
            {
                Debug.LogWarning("[UIFlipbookPreview] " + flipbook.name + " has no Image to draw into.", flipbook);
                return;
            }

            // Record BEFORE the first frame is written, so the undo entry holds the resting sprite.
            Undo.RecordObject(image, "Flipbook Preview");

            capturedImage = image;
            capturedSprite = image.sprite;
            target = flipbook;

            UISpriteFlipbook.EditorSuppressEvents = true;

            flipbook.EditorBeginPreview(clip);

            lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;

            RepaintEverything();
        }

        /// <summary>Stop the running preview and put the sprite back.</summary>
        internal static void End()
        {
            if (target == null && capturedImage == null) return;

            EditorApplication.update -= Tick;
            UISpriteFlipbook.EditorSuppressEvents = false;

            if (target != null)
            {
                target.EditorEndPreview();
            }

            if (capturedImage != null)
            {
                capturedImage.sprite = capturedSprite;

                // The restore went through a plain property setter, which does not mark the scene
                // or a prefab instance override as needing a resave on its own.
                EditorUtility.SetDirty(capturedImage);
            }

            target = null;
            capturedImage = null;
            capturedSprite = null;

            RepaintEverything();
        }

        /// <summary>Scrubber: hold one frame.</summary>
        internal static void SetFrame(int index)
        {
            if (target == null) return;

            target.EditorSetFrame(index);
            RepaintEverything();
        }

        /// <summary>Scrubber released: carry on from where it was left.</summary>
        internal static void Resume()
        {
            if (target == null) return;

            target.EditorResume();
            lastTime = EditorApplication.timeSinceStartup;
        }

        private static void Tick()
        {
            if (target == null || target.EditorImage == null)
            {
                // The object was deleted, or its Image was, while previewing.
                End();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Min((float)(now - lastTime), MaxDelta);
            lastTime = now;

            target.EditorTick(delta);

            RepaintEverything();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Entering play mode reloads the scene from disk; leaving one reloads the domain.
            // Either way the preview has to be unwound first so the restore actually lands.
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode)
            {
                End();
            }
        }

        private static void RepaintEverything()
        {
            // Scene view alone is not enough: UI is normally judged in the Game view, and that does
            // not redraw on its own in edit mode just because a sprite changed.
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
