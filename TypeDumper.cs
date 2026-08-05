using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OperatorPhone.Diagnostics
{
    /// <summary>
    /// Probe v2. Two questions:
    ///
    /// 1. How does InputLayer read input? If it calls UnityEngine.Input internally, the
    ///    existing Harmony prefixes already work. If it caches state in its own update,
    ///    they don't and we patch that method instead.
    /// 2. Is Michsky.DreamOS actually loaded? It's an in-game-OS UI kit — if its prefabs
    ///    are live we can build the phone from assets OPERATOR already ships.
    /// </summary>
    internal static class TypeDumper
    {
        private static readonly string[] Targets =
        {
            "InputLayer",
            "InputKey",
            "Keybind",
            "KeybindAssignment",
            "MasterController",
            "GameManager"
        };

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== OperatorPhone type dump ===");
            sb.AppendLine($"Unity {Application.unityVersion}\n");

            foreach (var t in Targets)
                DumpType(sb, t);

            DumpDreamOs(sb);

            var text = sb.ToString();
            PhoneMod.Log.Msg($"Type dump complete ({text.Length} chars) — see file.");

            try
            {
                var path = Path.Combine(UserDataDir(), "type_dump.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text);
                PhoneMod.Log.Msg($"Written to {path}");
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"Write failed: {e.Message}");
            }
        }

        private static void DumpType(StringBuilder sb, string typeName)
        {
            sb.AppendLine($"\n{'='.ToString().PadRight(60, '=')}");
            sb.AppendLine($"TYPE: {typeName}");
            sb.AppendLine($"{'='.ToString().PadRight(60, '=')}");

            Il2CppSystem.Type type = null;
            try
            {
                foreach (var asm in Il2CppSystem.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType(typeName);
                        if (t != null) { type = t; break; }
                    }
                    catch { }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"  lookup failed: {e.Message}");
                return;
            }

            if (type == null)
            {
                sb.AppendLine("  NOT FOUND");
                return;
            }

            sb.AppendLine($"  Base: {type.BaseType?.FullName ?? "?"}");

            // Static fields first — a singleton instance field is what we need to reach
            // the live object at runtime.
            sb.AppendLine("\n  -- Static fields --");
            TryEach(sb, () =>
            {
                foreach (var f in type.GetFields(
                    Il2CppSystem.Reflection.BindingFlags.Static |
                    Il2CppSystem.Reflection.BindingFlags.Public |
                    Il2CppSystem.Reflection.BindingFlags.NonPublic))
                {
                    sb.AppendLine($"    {f.FieldType?.Name} {f.Name}");
                }
            });

            sb.AppendLine("\n  -- Instance fields --");
            TryEach(sb, () =>
            {
                foreach (var f in type.GetFields(
                    Il2CppSystem.Reflection.BindingFlags.Instance |
                    Il2CppSystem.Reflection.BindingFlags.Public |
                    Il2CppSystem.Reflection.BindingFlags.NonPublic))
                {
                    sb.AppendLine($"    {f.FieldType?.Name} {f.Name}");
                }
            });

            sb.AppendLine("\n  -- Methods --");
            TryEach(sb, () =>
            {
                foreach (var m in type.GetMethods(
                    Il2CppSystem.Reflection.BindingFlags.Instance |
                    Il2CppSystem.Reflection.BindingFlags.Static |
                    Il2CppSystem.Reflection.BindingFlags.Public |
                    Il2CppSystem.Reflection.BindingFlags.NonPublic |
                    Il2CppSystem.Reflection.BindingFlags.DeclaredOnly))
                {
                    var ps = new StringBuilder();
                    try
                    {
                        var parms = m.GetParameters();
                        for (var i = 0; i < parms.Length; i++)
                        {
                            if (i > 0) ps.Append(", ");
                            ps.Append($"{parms[i].ParameterType?.Name} {parms[i].Name}");
                        }
                    }
                    catch { ps.Append("?"); }

                    var stat = m.IsStatic ? "static " : "";
                    sb.AppendLine($"    {stat}{m.ReturnType?.Name} {m.Name}({ps})");
                }
            });
        }

        private static void DumpDreamOs(StringBuilder sb)
        {
            sb.AppendLine($"\n{'='.ToString().PadRight(60, '=')}");
            sb.AppendLine("DREAMOS ASSETS (loaded objects, not just compiled types)");
            sb.AppendLine($"{'='.ToString().PadRight(60, '=')}");

            // Compiled-in but never loaded is useless to us. This checks what's actually
            // in memory and therefore reusable.
            try
            {
                var gos = Resources.FindObjectsOfTypeAll<GameObject>();
                var hits = 0;
                foreach (var go in gos)
                {
                    if (go == null) continue;
                    var n = go.name ?? "";
                    if (n.IndexOf("phone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("dreamos", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("messag", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine($"  GameObject: {n}  (scene: {go.scene.name})");
                        if (++hits >= 100) { sb.AppendLine("  ...capped"); break; }
                    }
                }
                sb.AppendLine($"  matches: {hits} of {gos.Length} loaded GameObjects");
            }
            catch (Exception e)
            {
                sb.AppendLine($"  scan failed: {e.Message}");
            }
        }

        private static void TryEach(StringBuilder sb, Action a)
        {
            try { a(); }
            catch (Exception e) { sb.AppendLine($"    <failed: {e.Message}>"); }
        }

        private static string UserDataDir()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName
                       ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "UserData", "OperatorPhone");
        }
    }
}
