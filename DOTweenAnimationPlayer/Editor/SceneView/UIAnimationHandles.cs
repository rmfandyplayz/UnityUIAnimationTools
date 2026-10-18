// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, October 2026,
// to a written design brief by the project author.
// See README.md in the folder above for usage.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// The drawing the Scene-view gizmos and the path editor share, so the two look like one tool: the
    /// ring a start is marked with and the dot an end is, the white ring round whichever is under the
    /// mouse, the labels, and lines in the three styles an endpoint's mode is shown by - solid for
    /// Absolute, dashed for Baseline, dotted for Current.
    ///
    /// Highlighted draws everything with a dark halo underneath, for the selected step: a bright line on a
    /// dark one stands out on any background, where inverting the pixels would vanish on mid-grey.
    /// </summary>
    internal static class UIAnimationHandles
    {
        public enum LineStyle
        {
            Solid = 0,
            Dashed = 1,
            Dotted = 2,
        }

        /// <summary>Absolute solid, Baseline dashed, Current dotted.</summary>
        public static LineStyle StyleOf(UIAnimationEndpointMode mode)
        {
            switch (mode)
            {
                case UIAnimationEndpointMode.Baseline: return LineStyle.Dashed;
                case UIAnimationEndpointMode.Current: return LineStyle.Dotted;
                default: return LineStyle.Solid;
            }
        }

        /// <summary>Set around drawing the selected step, so all of it gets a halo.</summary>
        public static bool Highlighted;

        private static readonly Color HaloColor = new Color(0f, 0f, 0f, 0.8f);

        // How much wider than the line its halo is, in points, and how far the halo runs past each end of
        // a dash so the dash's ends are framed too.
        private const float HaloWidth = 3f;
        private const float HaloOverrun = 1.5f;

        // ---------------------------------------------------------------- lines

        private static readonly List<float> lengths = new List<float>();
        private static readonly List<Vector2> pieces = new List<Vector2>();
        private static Vector3[] buffer = new Vector3[64];

        public static void PolyLine(Vector3[] points, float width, LineStyle style)
        {
            PolyLine(points, points.Length, width, style);
        }

        public static void PolyLine(List<Vector3> points, float width, LineStyle style)
        {
            if (Event.current.type != EventType.Repaint || points.Count < 2) return;
            PolyLine(points.ToArray(), points.Count, width, style);
        }

        /// <summary>
        /// A line through world points, in Handles.color. Dashes and dots are measured on screen, so they
        /// keep their size at any zoom, and the pattern runs on round corners rather than restarting.
        /// Repaint only.
        /// </summary>
        public static void PolyLine(Vector3[] points, int count, float width, LineStyle style)
        {
            if (Event.current.type != EventType.Repaint || count < 2) return;

            Color color = Handles.color;

            lengths.Clear();
            lengths.Add(0f);

            Vector2 previous = HandleUtility.WorldToGUIPoint(points[0]);
            for (int i = 1; i < count; i++)
            {
                Vector2 next = HandleUtility.WorldToGUIPoint(points[i]);
                lengths.Add(lengths[i - 1] + Vector2.Distance(previous, next));
                previous = next;
            }

            float total = lengths[count - 1];

            float on;
            float off;
            Pattern(style, width, out on, out off);

            pieces.Clear();

            if (style == LineStyle.Solid || total < on + off)
            {
                pieces.Add(new Vector2(0f, total));
            }
            else
            {
                for (float s = 0f; s < total; s += on + off) pieces.Add(new Vector2(s, Mathf.Min(total, s + on)));
            }

            if (Highlighted)
            {
                Handles.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * color.a);

                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector2 piece = pieces[i];
                    float extra = style == LineStyle.Solid ? 0f : HaloOverrun;
                    Piece(points, count, Mathf.Max(0f, piece.x - extra), Mathf.Min(total, piece.y + extra), width + HaloWidth);
                }

                Handles.color = color;
            }

            for (int i = 0; i < pieces.Count; i++) Piece(points, count, pieces[i].x, pieces[i].y, width);
        }

        /// <summary>
        /// On and off lengths in points. A dot is as long as the line is wide, so it reads as a dot; a dash
        /// is a few times longer than its gap, so the two styles can't be mistaken for each other.
        /// </summary>
        private static void Pattern(LineStyle style, float width, out float on, out float off)
        {
            if (style == LineStyle.Dotted)
            {
                on = Mathf.Max(1.5f, width);
                off = Mathf.Max(4f, width * 1.7f);
            }
            else
            {
                on = Mathf.Max(8f, width * 3.5f);
                off = Mathf.Max(5f, width * 2f);
            }
        }

        /// <summary>The stretch of the line between two distances along it on screen, as one polyline.</summary>
        private static void Piece(Vector3[] points, int count, float from, float to, float width)
        {
            if (to - from <= 0.01f) return;

            int used = 0;
            Ensure(count + 2);

            for (int i = 1; i < count; i++)
            {
                float a = lengths[i - 1];
                float b = lengths[i];

                if (b < from) continue;
                if (a > to) break;

                float span = b - a;

                if (used == 0) buffer[used++] = span > 0f ? Vector3.Lerp(points[i - 1], points[i], (from - a) / span) : points[i - 1];

                if (b <= to)
                {
                    buffer[used++] = points[i];
                }
                else
                {
                    buffer[used++] = span > 0f ? Vector3.Lerp(points[i - 1], points[i], (to - a) / span) : points[i];
                    break;
                }
            }

            if (used >= 2) Handles.DrawAAPolyLine(width, used, buffer);
        }

        private static void Ensure(int size)
        {
            if (buffer.Length < size) buffer = new Vector3[size * 2];
        }

        /// <summary>A filled triangle, with a halo when highlighted. Repaint only.</summary>
        public static void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (Highlighted)
            {
                Color color = Handles.color;
                Vector3 middle = (a + b + c) / 3f;

                Handles.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * color.a);
                Handles.DrawAAConvexPolygon(middle + (a - middle) * 1.6f, middle + (b - middle) * 1.6f, middle + (c - middle) * 1.6f);
                Handles.color = color;
            }

            Handles.DrawAAConvexPolygon(a, b, c);
        }

        // ---------------------------------------------------------------- endpoint caps

        /// <summary>The ring a start is drawn with and the dot an end is, as a fraction of the handle size.</summary>
        public const float StartSize = 0.05f;
        public const float EndSize = 0.045f;

        // Pickable a little beyond what is drawn, since the dots are small.
        private const float PickScale = 1.6f;

        /// <summary>Hovered with nothing held, or held.</summary>
        public static bool IsLit(int id)
        {
            return id != 0 && (GUIUtility.hotControl == id || (GUIUtility.hotControl == 0 && HandleUtility.nearestControl == id));
        }

        public static bool IsHeld(int id)
        {
            return id != 0 && GUIUtility.hotControl == id;
        }

        /// <summary>A ring: where a step starts.</summary>
        public static void WireCap(int id, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size * PickScale));
            }
            else if (eventType == EventType.Repaint)
            {
                Vector3 normal = rotation * Vector3.forward;

                if (Highlighted)
                {
                    Color color = Handles.color;
                    Handles.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * color.a);
                    Handles.DrawWireDisc(position, normal, size, 2f + HaloWidth);
                    Handles.color = color;
                }

                Handles.DrawWireDisc(position, normal, size, 2f);
                if (IsLit(id)) DrawLit(position, rotation, size);
            }
        }

        /// <summary>A dot: where a step ends.</summary>
        public static void SolidCap(int id, Vector3 position, Quaternion rotation, float size, EventType eventType)
        {
            if (eventType == EventType.Layout)
            {
                HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size * PickScale));
            }
            else if (eventType == EventType.Repaint)
            {
                Vector3 normal = rotation * Vector3.forward;

                if (Highlighted)
                {
                    Color color = Handles.color;
                    Handles.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * color.a);
                    Handles.DrawSolidDisc(position, normal, size * 1.45f);
                    Handles.color = color;
                }

                Handles.DrawSolidDisc(position, normal, size);
                if (IsLit(id)) DrawLit(position, rotation, size);
            }
        }

        /// <summary>A white ring round a hovered or held endpoint.</summary>
        public static void DrawLit(Vector3 position, Quaternion rotation, float size)
        {
            Color previous = Handles.color;
            Handles.color = Color.white;
            Handles.DrawWireDisc(position, rotation * Vector3.forward, size * 1.7f, 2f);
            Handles.color = previous;
        }

        /// <summary>
        /// The resize arrow a hovered or held edge or corner shows under the mouse, picked by which way the
        /// handle moves on screen, as Unity's Rect Tool does.
        /// </summary>
        public static void ResizeCursor(int id, Vector3 world, Vector3 direction)
        {
            if (Event.current.type != EventType.Repaint || !IsLit(id)) return;

            Vector2 at = HandleUtility.WorldToGUIPoint(world);
            Vector2 screen = HandleUtility.WorldToGUIPoint(world + direction) - at;
            if (screen.sqrMagnitude < 1e-6f) return;

            // GUI y runs down the screen.
            float angle = Mathf.Repeat(Mathf.Atan2(-screen.y, screen.x) * Mathf.Rad2Deg, 180f);

            MouseCursor cursor;
            if (angle < 22.5f || angle >= 157.5f) cursor = MouseCursor.ResizeHorizontal;
            else if (angle < 67.5f) cursor = MouseCursor.ResizeUpRight;
            else if (angle < 112.5f) cursor = MouseCursor.ResizeVertical;
            else cursor = MouseCursor.ResizeUpLeft;

            Handles.BeginGUI();
            var rect = new Rect(0f, 0f, 40f, 40f) { center = Event.current.mousePosition };
            EditorGUIUtility.AddCursorRect(rect, cursor, id);
            Handles.EndGUI();
        }

        // ---------------------------------------------------------------- labels

        private static GUIStyle labelStyle;

        /// <summary>
        /// A name, lifted clear of a point by a fixed number of pixels. Drawn with a dark shadow because a
        /// start point often sits on the object itself, and white text on a white Image is simply not there.
        /// </summary>
        public static void Label(Vector3 world, string text, float liftPixels)
        {
            if (Event.current.type != EventType.Repaint) return;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                };
            }

            // At least 80 wide, so the path editor's short labels sit exactly where they always have.
            float width = Mathf.Max(80f, labelStyle.CalcSize(new GUIContent(text)).x + 8f);

            Vector2 at = HandleUtility.WorldToGUIPoint(world);
            var rect = new Rect(at.x - width * 0.5f, at.y - 9f - liftPixels, width, 18f);

            Handles.BeginGUI();

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, labelStyle);
            GUI.color = previous;
            GUI.Label(rect, text, labelStyle);

            Handles.EndGUI();
        }
    }
}
