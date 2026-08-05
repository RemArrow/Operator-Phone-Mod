using HarmonyLib;
using UnityEngine;

namespace OperatorPhone.Input
{
    /// <summary>
    /// Suppresses legacy UnityEngine.Input while the phone owns focus.
    ///
    /// Only works if OPERATOR reads legacy Input directly. If the probe reports the new
    /// Input System package, or a custom manager that caches state, these patches are
    /// dead weight and M0 needs a different approach — patch the game's own poll method
    /// instead. Do not assume this works because it compiles.
    /// </summary>
    internal static class LegacyInputPatches
    {
        /// <summary>
        /// Keys the mod itself needs to keep reading even while suppressing everything else.
        /// Without this the toggle key gets swallowed by our own patch and the phone
        /// can never be closed.
        /// </summary>
        private static bool IsModKey(KeyCode k) =>
            k == ModConfig.ToggleKey || k == ModConfig.ProbeKey;

        private static bool Suppress => ModConfig.BlockGameInput && InputGate.Captured;

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), typeof(KeyCode))]
        private static class GetKeyPatch
        {
            private static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!Suppress || IsModKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown), typeof(KeyCode))]
        private static class GetKeyDownPatch
        {
            private static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!Suppress || IsModKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyUp), typeof(KeyCode))]
        private static class GetKeyUpPatch
        {
            private static bool Prefix(KeyCode key, ref bool __result)
            {
                if (!Suppress || IsModKey(key)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetAxis))]
        private static class GetAxisPatch
        {
            private static bool Prefix(ref float __result)
            {
                if (!Suppress) return true;
                __result = 0f;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetAxisRaw))]
        private static class GetAxisRawPatch
        {
            private static bool Prefix(ref float __result)
            {
                if (!Suppress) return true;
                __result = 0f;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButton))]
        private static class GetMouseButtonPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (!Suppress) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonDown))]
        private static class GetMouseButtonDownPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (!Suppress) return true;
                __result = false;
                return false;
            }
        }
    }
}
