// -----------------------------------------------------------------------------
// UI Animation Utility
//
// AI-GENERATED. Authored by Claude (Anthropic) via Claude Code, September 2026,
// to a written design brief by the project author.
// See the README.md beside this file for usage.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace rmf_claude.DOTweenUI
{

    /// <summary>
    /// What a Custom Property step tweens its member as. Written by the Property dropdown together
    /// with the member itself, so a step knows which From / To fields it uses even where there is
    /// nothing to look the member up on - a shared asset with no Preview On player.
    ///
    /// Serialized as an integer - see the note on UIAnimationStepType. Numbers are the contract.
    /// </summary>
    public enum UIAnimationPropertyKind
    {
        Float = 0,
        Int = 1,
        Vector2 = 2,
        Vector3 = 3,
        Color = 4,

        /// <summary>A string, typed out from From to To - DOTween's own string tween, the one DOText uses.</summary>
        Text = 5,

        /// <summary>A string that holds a number, counted from From to To and written back with a format.</summary>
        NumberText = 6,
    }

    /// <summary>One member a Custom Property step can drive, as the Property dropdown lists it.</summary>
    public struct UIAnimationPropertyOption
    {
        public Type ComponentType;
        public string Member;
        public UIAnimationPropertyKind Kind;
    }

    /// <summary>
    /// A resolved Custom Property: the component, and a typed getter and setter for one of its
    /// members. Built once per Resolve, so a playing tween never looks anything up by name.
    ///
    /// Properties go through Delegate.CreateDelegate on their accessors - the same call UnityEvent
    /// makes for a persistent listener, so it is known to work on every platform Unity builds for -
    /// which leaves a property tween with no reflection or boxing per frame. Fields go through
    /// FieldInfo, which boxes; a public field is the rarer case and a UI tween is a handful of calls.
    /// </summary>
    public sealed class UIAnimationMember
    {
        public readonly Component Component;
        public readonly UIAnimationPropertyKind Kind;

        private readonly Delegate getter;
        private readonly Delegate setter;

        public UIAnimationMember(Component component, MemberInfo member, UIAnimationPropertyKind kind)
        {
            Component = component;
            Kind = kind;

            switch (kind)
            {
                case UIAnimationPropertyKind.Float:
                    getter = MakeGetter<float>(component, member);
                    setter = MakeSetter<float>(component, member);
                    break;

                case UIAnimationPropertyKind.Int:
                    getter = MakeGetter<int>(component, member);
                    setter = MakeSetter<int>(component, member);
                    break;

                case UIAnimationPropertyKind.Vector2:
                    getter = MakeGetter<Vector2>(component, member);
                    setter = MakeSetter<Vector2>(component, member);
                    break;

                case UIAnimationPropertyKind.Vector3:
                    getter = MakeGetter<Vector3>(component, member);
                    setter = MakeSetter<Vector3>(component, member);
                    break;

                case UIAnimationPropertyKind.Color:
                    getter = MakeGetter<Color>(component, member);
                    setter = MakeSetter<Color>(component, member);
                    break;

                default:
                    getter = MakeGetter<string>(component, member);
                    setter = MakeSetter<string>(component, member);
                    break;
            }
        }

        /// <summary>
        /// The member's getter. T is the member's own type - string for both Text and NumberText -
        /// and asking for any other one throws, which UIAnimationProperties.TryLocate already rules out.
        /// </summary>
        public Func<T> Getter<T>()
        {
            return (Func<T>)getter;
        }

        /// <summary>The member's setter. Same rule as Getter.</summary>
        public Action<T> Setter<T>()
        {
            return (Action<T>)setter;
        }

        /// <summary>False once the component has been destroyed.</summary>
        public bool IsAlive
        {
            get { return Component != null; }
        }

        private static Func<T> MakeGetter<T>(Component component, MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null)
            {
                return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), component, property.GetGetMethod());
            }

            var field = (FieldInfo)member;
            return () => (T)field.GetValue(component);
        }

        private static Action<T> MakeSetter<T>(Component component, MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null)
            {
                return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), component, property.GetSetMethod());
            }

            var field = (FieldInfo)member;
            return value => field.SetValue(component, value);
        }
    }

    /// <summary>
    /// Finds the members a Custom Property step can drive. Playback, the Inspector's Property
    /// dropdown and its type check all go through here, so what the dropdown offers is exactly what
    /// playback will accept - never a copy of the rules that can drift.
    ///
    /// A member qualifies when it is public, instance, readable AND writable, not [Obsolete], not an
    /// indexer, and a float, int, Vector2, Vector3, Color or string. Members declared on
    /// UnityEngine.Object, Component, Behaviour and MonoBehaviour are left out: name, tag and friends
    /// would otherwise turn up under every component and none of them is anything to tween.
    ///
    /// Components are stored by their type's FullName and matched against what is on the object,
    /// rather than looked up with Type.GetType, so a script moving to another assembly does not break
    /// a step that names it.
    /// </summary>
    public static class UIAnimationProperties
    {
        private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private const BindingFlags Visible = BindingFlags.Public | BindingFlags.Instance;

        private const string NotPicked = "No Property picked - choose one from the Property dropdown.";

        // Reused so resolving and the per-repaint type check do not allocate a component array.
        private static readonly List<Component> components = new List<Component>();

        /// <summary>True for the value types a member can have and still be tweened.</summary>
        public static bool IsSupported(Type valueType)
        {
            return valueType == typeof(float)
                || valueType == typeof(int)
                || valueType == typeof(Vector2)
                || valueType == typeof(Vector3)
                || valueType == typeof(Color)
                || valueType == typeof(string);
        }

        /// <summary>True when a member of this value type can be tweened as this kind.</summary>
        public static bool Accepts(Type valueType, UIAnimationPropertyKind kind)
        {
            switch (kind)
            {
                case UIAnimationPropertyKind.Float: return valueType == typeof(float);
                case UIAnimationPropertyKind.Int: return valueType == typeof(int);
                case UIAnimationPropertyKind.Vector2: return valueType == typeof(Vector2);
                case UIAnimationPropertyKind.Vector3: return valueType == typeof(Vector3);
                case UIAnimationPropertyKind.Color: return valueType == typeof(Color);
                case UIAnimationPropertyKind.Text:
                case UIAnimationPropertyKind.NumberText: return valueType == typeof(string);
                default: return false;
            }
        }

        /// <summary>The type a field holds or a property returns.</summary>
        public static Type ValueTypeOf(MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null) return property.PropertyType;

            var field = member as FieldInfo;
            return field != null ? field.FieldType : null;
        }

        /// <summary>
        /// The tweenable member of this name, or null. Walks up from the component's own type one
        /// declaration at a time, which is what copes with an override that only replaces a getter -
        /// that declaration has no setter, so the walk carries on up to the one that does - and with a
        /// name declared again with `new`, which a flat lookup reports as ambiguous.
        /// </summary>
        public static MemberInfo FindMember(Type componentType, string memberName)
        {
            if (componentType == null || string.IsNullOrEmpty(memberName)) return null;

            for (Type type = componentType; type != null && !IsEngineBase(type); type = type.BaseType)
            {
                PropertyInfo property = null;

                // More than one property of one name in a single declaration is an overloaded indexer,
                // which is not tweenable anyway.
                try { property = type.GetProperty(memberName, Declared); }
                catch (AmbiguousMatchException) { }

                if (property != null && IsTweenable(property)) return property;

                FieldInfo field = type.GetField(memberName, Declared);
                if (field != null && IsTweenable(field)) return field;
            }

            return null;
        }

        /// <summary>The first component on the object whose type has this full name, or null.</summary>
        public static Component FindComponent(GameObject host, string componentType)
        {
            if (host == null || string.IsNullOrEmpty(componentType)) return null;

            host.GetComponents(components);

            Component found = null;

            for (int i = 0; i < components.Count; i++)
            {
                // A missing script shows up as a null component.
                if (components[i] != null && components[i].GetType().FullName == componentType)
                {
                    found = components[i];
                    break;
                }
            }

            components.Clear();
            return found;
        }

        /// <summary>
        /// Finds the component and member a Custom Property step names on host, and checks the member
        /// is still the type the step was set up for. Returns false with a reason when anything is
        /// wrong - or with no reason when host is null, since a missing object is reported by whoever
        /// resolved it (the Target Path check, or the ordinary missing-target warning).
        /// </summary>
        public static bool TryLocate(GameObject host, string componentType, string memberName, UIAnimationPropertyKind kind,
            out Component component, out MemberInfo member, out string problem)
        {
            component = null;
            member = null;
            problem = null;

            if (string.IsNullOrEmpty(componentType) || string.IsNullOrEmpty(memberName))
            {
                problem = NotPicked;
                return false;
            }

            if (host == null) return false;

            string shortName = ShortTypeName(componentType);

            Component found = FindComponent(host, componentType);
            if (found == null)
            {
                problem = "'" + host.name + "' has no " + shortName + ", so this step does nothing.";
                return false;
            }

            MemberInfo info = FindMember(found.GetType(), memberName);
            if (info == null)
            {
                problem = shortName + " has no public property or field '" + memberName +
                          "' that can be tweened - this step is skipped.";
                return false;
            }

            Type valueType = ValueTypeOf(info);
            if (!Accepts(valueType, kind))
            {
                problem = shortName + "." + memberName + " is a " + TypeLabel(valueType) + ", but this step was set up for a " +
                          TypeLabel(kind) + " - pick it again from the Property dropdown.";
                return false;
            }

            component = found;
            member = info;
            return true;
        }

        /// <summary>
        /// Every member the Property dropdown offers for an object: its components in the order the
        /// Inspector shows them, each type once, members alphabetical within one. A string member is
        /// listed twice, once for each way it can be tweened.
        /// </summary>
        public static void Collect(GameObject host, List<UIAnimationPropertyOption> into)
        {
            into.Clear();
            if (host == null) return;

            var seenTypes = new List<Type>();

            host.GetComponents(components);

            for (int c = 0; c < components.Count; c++)
            {
                if (components[c] == null) continue;

                Type type = components[c].GetType();
                if (seenTypes.Contains(type)) continue;
                seenTypes.Add(type);

                CollectMembers(type, into);
            }

            components.Clear();
        }

        /// <summary>One component type's part of Collect: its members, alphabetical, appended to into.</summary>
        public static void CollectMembers(Type componentType, List<UIAnimationPropertyOption> into)
        {
            var names = new List<string>();

            foreach (PropertyInfo property in componentType.GetProperties(Visible))
            {
                if (!names.Contains(property.Name)) names.Add(property.Name);
            }

            foreach (FieldInfo field in componentType.GetFields(Visible))
            {
                if (!names.Contains(field.Name)) names.Add(field.Name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);

            for (int n = 0; n < names.Count; n++)
            {
                // The same lookup playback does, so nothing is offered that would not then resolve.
                MemberInfo member = FindMember(componentType, names[n]);
                if (member == null) continue;

                Type valueType = ValueTypeOf(member);

                if (valueType == typeof(string))
                {
                    into.Add(new UIAnimationPropertyOption { ComponentType = componentType, Member = names[n], Kind = UIAnimationPropertyKind.Text });
                    into.Add(new UIAnimationPropertyOption { ComponentType = componentType, Member = names[n], Kind = UIAnimationPropertyKind.NumberText });
                }
                else
                {
                    into.Add(new UIAnimationPropertyOption { ComponentType = componentType, Member = names[n], Kind = KindOf(valueType) });
                }
            }
        }

        /// <summary>A member's current value, boxed. For the Inspector's Use Current Value button.</summary>
        public static object Read(Component component, MemberInfo member)
        {
            var property = member as PropertyInfo;
            if (property != null) return property.GetValue(component, null);

            var field = member as FieldInfo;
            return field != null ? field.GetValue(component) : null;
        }

        /// <summary>"TextMeshProUGUI.text", with " as number" when the text is counted as one.</summary>
        public static string Describe(string componentType, string memberName, UIAnimationPropertyKind kind)
        {
            string text = ShortTypeName(componentType) + "." + memberName;
            return kind == UIAnimationPropertyKind.NumberText ? text + " as number" : text;
        }

        /// <summary>The type name without its namespace: "TMPro.TextMeshProUGUI" reads "TextMeshProUGUI".</summary>
        public static string ShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return string.Empty;

            // Nested types are written Outer+Inner; the last part names it either way.
            int cut = Mathf.Max(fullName.LastIndexOf('.'), fullName.LastIndexOf('+'));
            return cut >= 0 ? fullName.Substring(cut + 1) : fullName;
        }

        /// <summary>The C# name of a supported value type, the way UnityEvent's dropdown writes it.</summary>
        public static string TypeLabel(Type valueType)
        {
            if (valueType == typeof(float)) return "float";
            if (valueType == typeof(int)) return "int";
            if (valueType == typeof(string)) return "string";
            return valueType != null ? valueType.Name : "nothing";
        }

        public static string TypeLabel(UIAnimationPropertyKind kind)
        {
            switch (kind)
            {
                case UIAnimationPropertyKind.Float: return "float";
                case UIAnimationPropertyKind.Int: return "int";
                case UIAnimationPropertyKind.Vector2: return "Vector2";
                case UIAnimationPropertyKind.Vector3: return "Vector3";
                case UIAnimationPropertyKind.Color: return "Color";
                default: return "string";
            }
        }

        /// <summary>
        /// The number a text shows, for a NumberText step that starts from it or measures from it.
        /// Only a text that is just a number reads back - thousands separators and a sign are fine,
        /// "Score: 120" is not - and anything else reads as 0. Tried in the player's culture first,
        /// since that is what FormatNumber writes in, then the invariant one.
        /// </summary>
        public static float ParseNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;
            float value;

            if (float.TryParse(text, Styles, CultureInfo.CurrentCulture, out value)) return value;
            if (float.TryParse(text, Styles, CultureInfo.InvariantCulture, out value)) return value;

            return 0f;
        }

        /// <summary>
        /// A number written with a .NET format string. An empty format is "0", and one .NET rejects
        /// for a float ("X", say) falls back to the plain number instead of throwing every frame.
        /// </summary>
        public static string FormatNumber(float value, string format)
        {
            try
            {
                return value.ToString(string.IsNullOrEmpty(format) ? "0" : format, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return value.ToString(CultureInfo.CurrentCulture);
            }
        }

        private static UIAnimationPropertyKind KindOf(Type valueType)
        {
            if (valueType == typeof(int)) return UIAnimationPropertyKind.Int;
            if (valueType == typeof(Vector2)) return UIAnimationPropertyKind.Vector2;
            if (valueType == typeof(Vector3)) return UIAnimationPropertyKind.Vector3;
            if (valueType == typeof(Color)) return UIAnimationPropertyKind.Color;
            if (valueType == typeof(string)) return UIAnimationPropertyKind.Text;
            return UIAnimationPropertyKind.Float;
        }

        private static bool IsTweenable(PropertyInfo property)
        {
            return property.GetIndexParameters().Length == 0
                && property.GetGetMethod() != null
                && property.GetSetMethod() != null
                && IsSupported(property.PropertyType)
                && !Attribute.IsDefined(property, typeof(ObsoleteAttribute));
        }

        private static bool IsTweenable(FieldInfo field)
        {
            return !field.IsInitOnly
                && !field.IsLiteral
                && IsSupported(field.FieldType)
                && !Attribute.IsDefined(field, typeof(ObsoleteAttribute));
        }

        /// <summary>Where the walk up a component's hierarchy stops. Nothing declared here is worth tweening.</summary>
        private static bool IsEngineBase(Type type)
        {
            return type == typeof(UnityEngine.Object)
                || type == typeof(Component)
                || type == typeof(Behaviour)
                || type == typeof(MonoBehaviour);
        }
    }
}
