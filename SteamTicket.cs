using System;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

// MelonLoader prefixes both the generated filename and the namespace with "Il2Cpp":
// the DLL is Il2Cppcom.rlabrecque.steamworks.net.dll and the namespace is
// Il2CppSteamworks. Everything below is written against this alias.
using SteamUser = Il2CppSteamworks.SteamUser;

namespace OperatorPhone.Identity
{
    /// <summary>
    /// Gets a Steam auth session ticket for PlayFab's LoginWithSteam.
    ///
    /// We do not initialise Steamworks ourselves — OPERATOR already owns the connection,
    /// and SteamAPI_Init is not safe to call twice in one process. We call through the
    /// binding the game already has running.
    ///
    /// Signature confirmed from the live game (Steamworks.NET, classic 3-arg form):
    ///   HAuthTicket GetAuthSessionTicket(byte[] pTicket, int cbMaxTicket, out uint pcbTicket)
    /// </summary>
    internal static class SteamTicket
    {
        private const int TicketBuffer = 1024;

        /// <summary>Hex-encoded ticket, or null on failure (reason logged).</summary>
        public static string Get()
        {
            try
            {
                var buffer = new Il2CppStructArray<byte>(TicketBuffer);
                uint written = 0;

                // If this line fails to compile with "cannot convert from 'out uint'",
                // change `out written` to `ref written` — Cpp2IL emits byref params
                // without preserving the out/ref distinction, so which one the interop
                // assembly exposes varies.
                var handle = SteamUser.GetAuthSessionTicket(buffer, TicketBuffer, out written);

                // k_HAuthTicketInvalid == 0. Steam returns this when the client isn't
                // logged in or the app isn't running under Steam.
                if (handle.m_HAuthTicket == 0)
                {
                    PhoneMod.Log.Error("Steam returned an invalid auth ticket handle.");
                    return null;
                }

                if (written == 0 || written > TicketBuffer)
                {
                    PhoneMod.Log.Error($"Implausible ticket length ({written}).");
                    return null;
                }

                var sb = new StringBuilder((int)written * 2);
                for (var i = 0; i < written; i++)
                    sb.Append(buffer[i].ToString("x2"));

                PhoneMod.Log.Msg($"Steam ticket acquired ({written} bytes).");
                return sb.ToString();
            }
            catch (Exception e)
            {
                PhoneMod.Log.Error($"GetAuthSessionTicket failed: {e}");
                return null;
            }
        }
    }
}