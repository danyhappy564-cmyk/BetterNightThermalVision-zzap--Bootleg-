using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.CameraControl;
using HarmonyLib;

namespace BetterVision
{
    /// <summary>
    /// Finds the two <see cref="PlayerCameraController"/> methods this mod hooks, without
    /// naming them.
    ///
    /// Up to SPT 4.0 they could be named: the client shipped obfuscated and they were
    /// <c>method_4</c> (re-applies the helmet night-vision template) and <c>method_5</c>
    /// (re-applies the T7 thermal template). SPT 4.1 ships a deobfuscated Assembly-CSharp and
    /// "method" is one of the prefixes its assembly tool rewrites, so both names are gone. The
    /// replacements are not published anywhere either - the tool derives them by matching
    /// signatures against a real-named reference assembly while it builds, and keeps no table.
    ///
    /// So resolve them by shape instead. Both are `public void ()`, and PlayerCameraController
    /// declares four of those, so the signature alone is not enough. What separates them is the
    /// item component each one reads: the night-vision method is the only one on the type that
    /// touches NightVisionComponent, and the thermal one the only one that touches
    /// ThermalVisionComponent. Those are the game's own public item-component types, not
    /// obfuscated names, so the fingerprint reads the same before and after the rename - and it
    /// also holds if a future build renumbers the methods, which a hardcoded "method_4" would
    /// silently get wrong.
    /// </summary>
    internal static class VisionTargets
    {
        internal static void ApplyDynamicPatches(Harmony harmony)
        {
            Patch(harmony, "NightVisionComponent", typeof(Patch_NV_Noise), "night vision noise");
            Patch(harmony, "ThermalVisionComponent", typeof(Patch_T7Thermal), "T7 thermal settings");
        }

        private static void Patch(Harmony harmony, string componentTypeName, Type patchClass, string what)
        {
            MethodInfo target = FindRefreshMethod(componentTypeName);
            if (target == null)
            {
                BetterVision.PluginLog.LogError(
                    $"Could not find the PlayerCameraController method that reads {componentTypeName}; " +
                    $"the {what} patch is not applied and those options will do nothing.");
                return;
            }

            MethodInfo postfix = AccessTools.Method(patchClass, "Postfix");
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            BetterVision.PluginLog.LogInfo($"Patched PlayerCameraController.{target.Name} for {what}.");
        }

        private static MethodInfo FindRefreshMethod(string componentTypeName)
        {
            // A local of the component's type is the precise signal - the game declares one in
            // both methods and uses it repeatedly, so the compiler has no reason to fold it
            // away. Reading locals also costs nothing beyond the reflection call.
            MethodInfo byLocal = Single(componentTypeName, DeclaresLocalOfType);
            if (byLocal != null)
                return byLocal;

            // Nothing matched, so fall back to "the method's body mentions the type at all".
            // Broader, and therefore only worth running once the precise pass has come up
            // empty, but it survives a build where the local was optimised out.
            return Single(componentTypeName, BodyMentionsType);
        }

        private static MethodInfo Single(string componentTypeName, Func<MethodInfo, string, bool> matches)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                                     | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            MethodInfo found = null;
            foreach (MethodInfo m in typeof(PlayerCameraController).GetMethods(flags))
            {
                if (m.IsAbstract || m.IsGenericMethodDefinition || m.IsSpecialName)
                    continue;
                if (m.ReturnType != typeof(void) || m.GetParameters().Length != 0)
                    continue;
                if (!matches(m, componentTypeName))
                    continue;

                // Two matches means the fingerprint stopped being unique, and picking either
                // one would patch the game at a point nobody checked. Refuse instead.
                if (found != null)
                {
                    BetterVision.PluginLog.LogError(
                        $"PlayerCameraController has more than one candidate for {componentTypeName} " +
                        $"({found.Name}, {m.Name}); refusing to guess.");
                    return null;
                }

                found = m;
            }

            return found;
        }

        private static bool DeclaresLocalOfType(MethodInfo method, string typeName)
        {
            MethodBody body;
            try
            {
                body = method.GetMethodBody();
            }
            catch
            {
                return false;
            }

            if (body == null)
                return false;

            foreach (LocalVariableInfo local in body.LocalVariables)
            {
                if (local.LocalType != null && local.LocalType.Name == typeName)
                    return true;
            }

            return false;
        }

        private static bool BodyMentionsType(MethodInfo method, string typeName)
        {
            IEnumerable<KeyValuePair<System.Reflection.Emit.OpCode, object>> instructions;
            try
            {
                instructions = PatchProcessor.ReadMethodBody(method);
            }
            catch
            {
                return false;
            }

            foreach (var instruction in instructions)
            {
                object operand = instruction.Value;

                if (operand is Type type && type.Name == typeName)
                    return true;

                if (operand is MemberInfo member
                    && member.DeclaringType != null
                    && member.DeclaringType.Name == typeName)
                    return true;
            }

            return false;
        }
    }
}
