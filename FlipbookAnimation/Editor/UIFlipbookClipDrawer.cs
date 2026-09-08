// -----------------------------------------------------------------------------
// Flipnote Style Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author. Not hand-written by the
// Twindrill Goose team. See the README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace rmf_claude.FlipbookAnimation
{
    /// <summary>
    /// Draws a clip as a bold summary header you can read collapsed - "Normal (4 frames · 4 FPS ·
    /// 1.00s · loop)" - over a body that only shows the timing fields the chosen mode actually uses.
    ///
    /// Five of these stack up on a UIFlipbookSelectable, so the collapsed row carrying the real
    /// information is most of the value here.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIFlipbookClip))]
    internal class UIFlipbookClipDrawer : PropertyDrawer
    {
        private const float ThumbnailStripHeight = 42f;
        private const float FpsFieldWidth = 54f;
        private const float SharedNoteHeight = 34f;

        private static GUIStyle boldFoldout;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            var pad = EditorGUIUtility.standardVerticalSpacing;

            var height = line;

            if (!property.isExpanded) return height;

            var shared = property.FindPropertyRelative("Shared");
            height += pad + line;

            // A shared clip is authored on the asset, so there is nothing else to edit here.
            if (shared != null && shared.objectReferenceValue != null)
            {
                return height + pad + SharedNoteHeight;
            }

            height += pad + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("Frames"), true);
            height += pad + ThumbnailStripHeight;
            height += pad + line; // FPS
            height += pad + line; // Loop Mode
            height += pad + line; // Timing

            switch ((UIFlipbookTiming)property.FindPropertyRelative("Timing").intValue)
            {
                case UIFlipbookTiming.RandomOffset:
                    height += pad + line + pad + line;
                    break;

                case UIFlipbookTiming.PerFrameDurations:
                    height += pad + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("FrameDurations"), true);
                    break;
            }

            return height + pad;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = EditorGUIUtility.singleLineHeight;
            var cursor = new Rect(position.x, position.y, position.width, line);

            property.isExpanded = EditorGUI.Foldout(
                cursor, property.isExpanded, Header(property, label), true, BoldFoldout());

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                DrawBody(position, cursor, property, line);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void DrawBody(Rect position, Rect cursor, SerializedProperty property, float line)
        {
            var shared = property.FindPropertyRelative("Shared");

            EditorGUI.PropertyField(Next(ref cursor, position, line), shared);

            if (shared.objectReferenceValue != null)
            {
                EditorGUI.HelpBox(
                    Next(ref cursor, position, SharedNoteHeight),
                    "Playing from the shared asset. Frames and timing are authored there; the " +
                    "settings below are ignored while this slot is filled.",
                    MessageType.Info);

                return;
            }

            var frames = property.FindPropertyRelative("Frames");
            EditorGUI.PropertyField(Next(ref cursor, position, EditorGUI.GetPropertyHeight(frames, true)), frames, true);

            DrawThumbnails(EditorGUI.IndentedRect(Next(ref cursor, position, ThumbnailStripHeight)), frames);

            DrawFPS(Next(ref cursor, position, line), property.FindPropertyRelative("FPS"));

            EditorGUI.PropertyField(Next(ref cursor, position, line), property.FindPropertyRelative("LoopMode"));

            var timing = property.FindPropertyRelative("Timing");
            EditorGUI.PropertyField(Next(ref cursor, position, line), timing);

            switch ((UIFlipbookTiming)timing.intValue)
            {
                case UIFlipbookTiming.RandomOffset:
                    EditorGUI.PropertyField(Next(ref cursor, position, line), property.FindPropertyRelative("RandomOffsetMin"));
                    EditorGUI.PropertyField(Next(ref cursor, position, line), property.FindPropertyRelative("RandomOffsetMax"));
                    break;

                case UIFlipbookTiming.PerFrameDurations:
                    var durations = property.FindPropertyRelative("FrameDurations");
                    EditorGUI.PropertyField(
                        Next(ref cursor, position, EditorGUI.GetPropertyHeight(durations, true)), durations, true);
                    break;
            }
        }

        /// <summary>
        /// FPS as a 1-12 slider PLUS a free number field. Unity's [Range] cannot do this - it clamps
        /// the typed value too - so the two halves are drawn by hand and only the slider clamps.
        /// </summary>
        private static void DrawFPS(Rect rect, SerializedProperty fps)
        {
            var content = new GUIContent("FPS", fps.tooltip);
            var field = EditorGUI.PrefixLabel(rect, content);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var sliderRect = new Rect(field.x, field.y, Mathf.Max(0f, field.width - FpsFieldWidth - 4f), field.height);
            var numberRect = new Rect(field.xMax - FpsFieldWidth, field.y, FpsFieldWidth, field.height);

            var value = fps.floatValue;
            var clamped = Mathf.Clamp(value, 1f, UIFlipbookClip.SliderMaxFPS);

            var dragged = GUI.HorizontalSlider(sliderRect, clamped, 1f, UIFlipbookClip.SliderMaxFPS);

            // Only adopt the slider when it actually moved. Without this, typing 30 into the field
            // would be slammed back to 12 on the very next repaint.
            if (!Mathf.Approximately(dragged, clamped))
            {
                value = dragged;
            }

            value = EditorGUI.FloatField(numberRect, value);

            EditorGUI.indentLevel = indent;

            fps.floatValue = Mathf.Max(UIFlipbookClip.MinFPS, value);
        }

        /// <summary>
        /// A single row of drawings, so you can tell at a glance whether the frames are in the right
        /// order. Deliberately one fixed-height row: a wrapping grid would need the inspector width
        /// inside GetPropertyHeight, which Unity does not hand to it.
        /// </summary>
        private static void DrawThumbnails(Rect rect, SerializedProperty frames)
        {
            if (Event.current.type != EventType.Repaint) return;

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.12f));

            if (frames.arraySize == 0)
            {
                EditorGUI.LabelField(rect, "  no frames", EditorStyles.miniLabel);
                return;
            }

            var cell = rect.height - 6f;
            var x = rect.x + 3f;
            var shown = 0;

            for (int i = 0; i < frames.arraySize; i++)
            {
                if (x + cell > rect.xMax - 26f) break;

                DrawSprite(new Rect(x, rect.y + 3f, cell, cell),
                    frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite);

                x += cell + 3f;
                shown++;
            }

            if (shown < frames.arraySize)
            {
                EditorGUI.LabelField(
                    new Rect(x + 2f, rect.y, rect.xMax - x - 2f, rect.height),
                    "+" + (frames.arraySize - shown),
                    EditorStyles.miniLabel);
            }
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0f, 0f, 0.15f));
                return;
            }

            var texture = sprite.texture;
            var source = sprite.textureRect;

            // Sprites are usually part of a sheet, so the thumbnail has to be the sprite's own
            // rectangle within the texture rather than the whole texture.
            var uv = new Rect(
                source.x / texture.width,
                source.y / texture.height,
                source.width / texture.width,
                source.height / texture.height);

            var aspect = source.height > 0f ? source.width / source.height : 1f;
            var fit = rect;

            if (aspect >= 1f)
            {
                fit.height = rect.width / aspect;
                fit.y += (rect.height - fit.height) * 0.5f;
            }
            else
            {
                fit.width = rect.height * aspect;
                fit.x += (rect.width - fit.width) * 0.5f;
            }

            GUI.DrawTextureWithTexCoords(fit, texture, uv, true);
        }

        // ---------------------------------------------------------------------------- header text

        private static GUIContent Header(SerializedProperty property, GUIContent label)
        {
            return new GUIContent(label.text + " (" + Summary(property) + ")", label.tooltip);
        }

        /// <summary>Everything inside the parentheses: "6 frames · 6 FPS · 1.00s · loop".</summary>
        private static string Summary(SerializedProperty property)
        {
            var shared = property.FindPropertyRelative("Shared");

            if (shared != null && shared.objectReferenceValue != null)
            {
                return "shared → " + shared.objectReferenceValue.name;
            }

            var frames = property.FindPropertyRelative("Frames").arraySize;

            if (frames == 0)
            {
                return "empty";
            }

            var fps = property.FindPropertyRelative("FPS").floatValue;
            var timing = (UIFlipbookTiming)property.FindPropertyRelative("Timing").intValue;
            var loop = (UIFlipbookLoopMode)property.FindPropertyRelative("LoopMode").intValue;

            var text = frames + (frames == 1 ? " frame" : " frames") +
                       " · " + Trim(fps) + " FPS" +
                       " · " + Duration(property, frames, fps, timing).ToString("0.00") + "s" +
                       " · " + LoopText(loop);

            if (timing == UIFlipbookTiming.RandomOffset) text += " · random";
            else if (timing == UIFlipbookTiming.PerFrameDurations) text += " · hand-timed";

            return text;
        }

        /// <summary>
        /// Mirrors UIFlipbookClip.TotalDuration, but off the serialized properties so the header can
        /// be drawn without boxing a copy of the clip on every repaint.
        /// </summary>
        private static float Duration(SerializedProperty property, int frames, float fps, UIFlipbookTiming timing)
        {
            var baseTime = 1f / (fps >= UIFlipbookClip.MinFPS ? fps : UIFlipbookClip.DefaultFPS);

            if (timing == UIFlipbookTiming.RandomOffset)
            {
                var min = property.FindPropertyRelative("RandomOffsetMin").floatValue;
                var max = property.FindPropertyRelative("RandomOffsetMax").floatValue;

                return Mathf.Max(UIFlipbookClip.MinFrameDuration, baseTime + (min + max) * 0.5f) * frames;
            }

            if (timing != UIFlipbookTiming.PerFrameDurations)
            {
                return baseTime * frames;
            }

            var durations = property.FindPropertyRelative("FrameDurations");
            var total = 0f;

            for (int i = 0; i < frames; i++)
            {
                var held = i < durations.arraySize ? durations.GetArrayElementAtIndex(i).floatValue : 0f;
                total += held > 0f ? held : baseTime;
            }

            return total;
        }

        private static string LoopText(UIFlipbookLoopMode mode)
        {
            switch (mode)
            {
                case UIFlipbookLoopMode.Loop: return "loop";
                case UIFlipbookLoopMode.PingPong: return "ping pong";
                default: return "once";
            }
        }

        private static string Trim(float value)
        {
            return Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");
        }

        // ------------------------------------------------------------------------------- plumbing

        private static Rect Next(ref Rect cursor, Rect position, float height)
        {
            cursor.y += cursor.height + EditorGUIUtility.standardVerticalSpacing;
            cursor.height = height;
            cursor.x = position.x;
            cursor.width = position.width;

            return cursor;
        }

        private static GUIStyle BoldFoldout()
        {
            if (boldFoldout == null)
            {
                // A COPY. Setting fontStyle on EditorStyles.foldout itself would bold every foldout
                // in the entire Editor, which is a genuinely confusing thing to ship.
                boldFoldout = new GUIStyle(EditorStyles.foldout);
                boldFoldout.fontStyle = FontStyle.Bold;
            }

            return boldFoldout;
        }
    }
}
