using System;

// MelonLoader prefixes both the generated filename and the namespace with "Il2Cpp":
// the DLL is Il2Cppcom.rlabrecque.steamworks.net.dll, namespace Il2CppSteamworks.
using SteamUser = Il2CppSteamworks.SteamUser;

namespace OperatorPhone.Identity
{
    /// <summary>
    /// Reads the SteamID of the account currently running the game.
    ///
    /// NOT used for authentication — a client asserting its own SteamID is trivially
    /// spoofable, which is exactly why auth goes through Steam OpenID in a browser
    /// instead. This is only used to notice when the linked account and the running
    /// account differ, e.g. after someone switches Steam users on a shared machine.
    ///
    /// (The auth-session-ticket path was abandoned: validating one requires a publisher
    /// Web API key scoped to the app, which only Vector Interactive can issue.)
    /// </summary>
    internal static class SteamIdentity
    {
        public static ulong GetCurrent()
        {
            try
            {
                var id = SteamUser.GetSteamID();
                return id.m_SteamID;
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"Could not read local SteamID: {e.Message}");
                return 0;
            }
        }
    }
}
