using System;
using System.Reflection;

namespace ClaudeModLib
{
    /// <summary>
    /// Read/write non-public members of game objects. Many useful game fields are private
    /// (e.g. Shop._sales, ProductDefinition.categoryProvider, Factory.availableRecipes).
    /// Members are searched through the whole class hierarchy; fields first, then properties.
    /// </summary>
    public static class Reflect
    {
        public const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static FieldInfo FindField(Type t, string name, bool isStatic = false)
        {
            for (; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, (isStatic ? AllStatic : All) | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        public static PropertyInfo FindProperty(Type t, string name, bool isStatic = false)
        {
            for (; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, (isStatic ? AllStatic : All) | BindingFlags.DeclaredOnly);
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>Gets field or property <paramref name="name"/>. Throws MissingMemberException if absent.</summary>
        public static object Get(object target, string name)
        {
            var f = FindField(target.GetType(), name);
            if (f != null) return f.GetValue(target);
            var p = FindProperty(target.GetType(), name);
            if (p != null && p.CanRead) return p.GetValue(target, null);
            throw new MissingMemberException(target.GetType().Name, name);
        }

        public static T Get<T>(object target, string name) { return (T)Get(target, name); }

        /// <summary>Like Get, but returns <paramref name="fallback"/> instead of throwing.</summary>
        public static T GetOr<T>(object target, string name, T fallback)
        {
            try { var v = Get(target, name); return v is T ? (T)v : fallback; } catch { return fallback; }
        }

        /// <summary>Sets field or property <paramref name="name"/>. Throws MissingMemberException if absent.</summary>
        public static void Set(object target, string name, object value)
        {
            var f = FindField(target.GetType(), name);
            if (f != null) { f.SetValue(target, value); return; }
            var p = FindProperty(target.GetType(), name);
            if (p != null && p.CanWrite) { p.SetValue(target, value, null); return; }
            throw new MissingMemberException(target.GetType().Name, name);
        }

        public static object GetStatic(Type type, string name)
        {
            var f = FindField(type, name, true);
            if (f != null) return f.GetValue(null);
            var p = FindProperty(type, name, true);
            if (p != null) return p.GetValue(null, null);
            throw new MissingMemberException(type.Name, name);
        }

        /// <summary>Invokes a (possibly non-public) instance method, matched by name and argument count.</summary>
        public static object Call(object target, string method, params object[] args)
        {
            args = args ?? new object[0];
            for (var t = target.GetType(); t != null; t = t.BaseType)
                foreach (var m in t.GetMethods(All | BindingFlags.DeclaredOnly))
                    if (m.Name == method && m.GetParameters().Length == args.Length)
                        return m.Invoke(target, args);
            throw new MissingMethodException(target.GetType().Name, method);
        }
    }
}
