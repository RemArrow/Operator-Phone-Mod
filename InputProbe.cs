using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace OperatorPhone.Diagnostics
{
    /// <summary>
    /// M0 tooling. Answers one question: how does OPERATOR read input?
    ///
    /// Run this before writing a single line of capture logic. The answer determines
    /// whether LegacyInputPatches is sufficient or whether the game's own input manager
    /// needs patching directly.
    /// </summary>
    internal static class InputProbe
    {
        private static readonly string[] Needles =
        {
            "input", "control", "binding", "keybind", "playerinput", "actionmap"
        };

        // Assemblies that will match the needles but tell us nothing useful.
        private static readonly string[] Noise =
        {
            "mscorlib", "System", "Il2CppInterop", "MelonLoader", "netstandard"
        };

        public static void Run()
        {
            var log = PhoneMod.Log;
            var sb = new StringBuilder();

            sb.AppendLine("=== OperatorPhone input probe ===");
            sb.AppendLine($"Unity: {Application.unityVersion}");
            sb.AppendLine($"Product: {Application.productName}");
            sb.AppendLine($"Cursor: lockState={Cursor.lockState} visible={Cursor.visible}");

            ProbeLegacy(sb);
            ProbeTypes(sb);

            var text = sb.ToString();
            log.Msg(text);

            try
            {
                var path = Path.Combine(UserDataDir(), "input_probe.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
                log.Msg($"Probe written to {path}");
            }
            catch (Exception e)
            {
                log.Warning($"Could not write probe file: {e.Message}");
            }
        }

        /// <summary>
        /// Derived from Application.dataPath rather than MelonEnvironment, which lives in
        /// MelonLoader.Utils on 0.6+ but is MelonUtils on 0.5.x. dataPath is
        /// "&lt;game&gt;/OPERATOR_Data", so its parent is the install root.
        /// </summary>
        private static string UserDataDir()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName
                       ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "UserData", "OperatorPhone");
        }

        private static void ProbeLegacy(StringBuilder sb)
        {
            sb.AppendLine("\n--- Legacy input ---");
            try
            {
                // If the legacy backend is disabled, touching these throws InvalidOperationException.
                // That exception IS the answer: the game is on the new Input System.
                var mp = UnityEngine.Input.mousePosition;
                sb.AppendLine($"UnityEngine.Input responsive. mousePosition={mp}");
                sb.AppendLine("Legacy backend ENABLED -> LegacyInputPatches may be viable.");
            }
            catch (Exception e)
            {
                sb.AppendLine($"UnityEngine.Input threw: {e.GetType().Name}: {e.Message}");
                sb.AppendLine("Legacy backend likely DISABLED -> patches are dead weight. " +
                              "Target the new Input System or the game's own manager.");
            }
        }

        private static void ProbeTypes(StringBuilder sb)
        {
            sb.AppendLine("\n--- Input-related types ---");

            var hits = new List<string>();
            var newInputSystem = false;

            try
            {
                var assemblies = Il2CppSystem.AppDomain.CurrentDomain.GetAssemblies();
                sb.AppendLine($"Assemblies loaded: {assemblies.Length}");

                foreach (var asm in assemblies)
                {
                    string asmName;
                    try { asmName = asm.GetName().Name; }
                    catch { continue; }

                    if (IsNoise(asmName)) continue;

                    if (asmName.IndexOf("Unity.InputSystem", StringComparison.OrdinalIgnoreCase) >= 0)
                        newInputSystem = true;

                    Il2CppSystem.Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        string full;
                        try { full = t.FullName; }
                        catch { continue; }
                        if (string.IsNullOrEmpty(full)) continue;

                        if (Matches(full))
                            hits.Add($"  [{asmName}] {full}");
                    }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"Type enumeration failed: {e}");
                return;
            }

            sb.AppendLine($"New Input System package present: {newInputSystem}");
            sb.AppendLine($"Matches: {hits.Count}");

            // Cap the dump — a full match list can be thousands of lines and will
            // make the MelonLoader console unusable.
            const int cap = 200;
            for (var i = 0; i < hits.Count && i < cap; i++)
                sb.AppendLine(hits[i]);

            if (hits.Count > cap)
                sb.AppendLine($"  ... {hits.Count - cap} more (see file, cap raised there is a TODO)");

            sb.AppendLine("\nNext step: find the type that OPERATOR polls each frame " +
                          "(look for a singleton-ish manager, not the Unity types) and " +
                          "patch its update/poll method rather than UnityEngine.Input.");
        }

        private static bool IsNoise(string name)
        {
            foreach (var n in Noise)
                if (name.StartsWith(n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool Matches(string fullName)
        {
            foreach (var n in Needles)
                if (fullName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}