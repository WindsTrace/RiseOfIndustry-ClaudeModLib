using System;
using System.Reflection;
using Harmony;

namespace ClaudeModLib
{
    /// <summary>
    /// Small helpers around the game's Harmony 1.x (0Harmony.dll) for patching game methods, including private
    /// methods of private nested classes (e.g. SettlementAdvancement+AcceptState.Initialize).
    /// Harmony injects patch parameters BY NAME: use the original parameter names, plus __instance / __result.
    /// </summary>
    public static class Patching
    {
        public const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Finds a method (any visibility). <paramref name="typePath"/> may name nested types: "Outer+Inner".</summary>
        public static MethodInfo Method(Type root, string typePath, string method)
        {
            var t = root;
            if (!string.IsNullOrEmpty(typePath))
                foreach (var part in typePath.Split('+'))
                {
                    t = t.GetNestedType(part, BindingFlags.Public | BindingFlags.NonPublic);
                    if (t == null) throw new MissingMemberException(root.Name, typePath);
                }
            var m = t.GetMethod(method, Any);
            if (m == null) throw new MissingMethodException(t.Name, method);
            return m;
        }

        public static MethodInfo Method(Type type, string method) { return Method(type, null, method); }

        /// <summary>
        /// Patches <paramref name="original"/> with static methods named <paramref name="prefix"/> / <paramref name="postfix"/>
        /// declared on <paramref name="patchClass"/> (either may be null). A prefix returning false skips the original.
        /// </summary>
        public static void Apply(HarmonyInstance harmony, MethodBase original, Type patchClass, string prefix, string postfix)
        {
            harmony.Patch(original,
                prefix != null ? new HarmonyMethod(patchClass.GetMethod(prefix, Any)) : null,
                postfix != null ? new HarmonyMethod(patchClass.GetMethod(postfix, Any)) : null);
        }
    }
}
