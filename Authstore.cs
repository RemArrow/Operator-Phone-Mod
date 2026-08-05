using System;
using System.IO;
using UnityEngine;

namespace OperatorPhone.Identity
{
    /// <summary>
    /// Persists the auth token between sessions so linking happens once per install
    /// rather than once per launch.
    ///
    /// Plain file, no encryption: anything the mod can decrypt at runtime, someone with
    /// filesystem access can decrypt too, so obfuscation would only be theatre. The
    /// token is scoped to this one service and expires, which is the actual mitigation.
    /// </summary>
    internal static class AuthStore
    {
        private const string FileName = "auth.txt";

        public static string Token { get; private set; }
        public static string Number { get; private set; }
        public static ulong LinkedSteamId { get; private set; }

        /// <summary>Photon UserId for this account (the opaque steam_hash). Set on connect.</summary>
        public static string AccountId { get; set; }

        public static bool HasToken => !string.IsNullOrEmpty(Token);

        public static void Load()
        {
            try
            {
                var path = FilePath();
                if (!File.Exists(path)) return;

                var lines = File.ReadAllLines(path);
                if (lines.Length > 0) Token = lines[0].Trim();
                if (lines.Length > 1) Number = lines[1].Trim();
                if (lines.Length > 2 && ulong.TryParse(lines[2].Trim(), out var sid))
                    LinkedSteamId = sid;

                if (HasToken) PhoneMod.Log.Msg($"Loaded stored link ({Number}).");
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"Could not read stored auth: {e.Message}");
            }
        }

        public static void Save(string token, string number, ulong steamId)
        {
            Token = token;
            Number = number;
            LinkedSteamId = steamId;

            try
            {
                var path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, new[] { token, number ?? "", steamId.ToString() });
            }
            catch (Exception e)
            {
                PhoneMod.Log.Error($"Could not persist auth: {e.Message}");
            }
        }

        public static void Clear()
        {
            Token = null;
            Number = null;
            LinkedSteamId = 0;
            try { File.Delete(FilePath()); } catch { }
        }

        private static string FilePath()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName
                       ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "UserData", "OperatorPhone", FileName);
        }
    }
}