using System;
using System.Collections;
using MelonLoader;
using OperatorPhone.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace OperatorPhone.Identity
{
    public enum IdentityState { Idle, NeedsLink, Linking, Authenticating, Ready, Failed }

    /// <summary>
    /// Auth is Steam OpenID via the user's browser, not an in-process Steam ticket:
    /// validating a ticket needs a publisher Web API key scoped to the app, which only
    /// the game's developer can issue.
    ///
    /// First launch:  link/start -> browser -> poll -> token + number, stored on disk.
    /// Later launches: claim with the stored token. No browser.
    /// </summary>
    internal static class IdentityService
    {
        private const float PollInterval = 2f;
        private const float PollTimeout = 300f;

        public static IdentityState State { get; private set; } = IdentityState.Idle;
        public static string Number { get; private set; }
        public static string LastError { get; private set; }

        public static event Action OnChanged;

        public static void Begin()
        {
            if (State == IdentityState.Linking || State == IdentityState.Authenticating) return;
            MelonCoroutines.Start(Run());
        }

        /// <summary>Called when the user activates the "Link Steam account" button.</summary>
        public static void StartLink()
        {
            if (State == IdentityState.Linking) return;
            MelonCoroutines.Start(LinkFlow());
        }

        public static void Unlink()
        {
            AuthStore.Clear();
            Number = null;
            Set(IdentityState.NeedsLink, null);
        }

        private static IEnumerator Run()
        {
            AuthStore.Load();

            if (!AuthStore.HasToken)
            {
                Set(IdentityState.NeedsLink, null);
                yield break;
            }

            // A different Steam account is running than the one linked. Don't silently
            // hand this session someone else's number — on a shared machine that would
            // deliver their messages to the wrong person.
            var current = SteamIdentity.GetCurrent();
            if (current != 0 && AuthStore.LinkedSteamId != 0 && current != AuthStore.LinkedSteamId)
            {
                PhoneMod.Log.Warning("Linked Steam account differs from the running one — relink required.");
                AuthStore.Clear();
                Set(IdentityState.NeedsLink, "account_changed");
                yield break;
            }

            Set(IdentityState.Authenticating, null);
            yield return Claim();
        }

        private static IEnumerator Claim()
        {
            var req = Post(Url("/v1/account/claim"), "{}");
            req.SetRequestHeader("Authorization", "Bearer " + AuthStore.Token);
            try
            {
                yield return req.SendWebRequest();
                var text = req.downloadHandler?.text;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    var code = MiniJson.GetString(text, "error") ?? req.responseCode.ToString();
                    PhoneMod.Log.Error($"Claim failed: {req.responseCode} {text}");

                    // Expired or revoked token: drop it and ask for a fresh link rather
                    // than retrying forever against a credential that will never work.
                    if (req.responseCode == 401 || req.responseCode == 404)
                    {
                        AuthStore.Clear();
                        Set(IdentityState.NeedsLink, code);
                    }
                    else Set(IdentityState.Failed, code);
                    yield break;
                }

                Number = MiniJson.GetString(text, "number");
                if (string.IsNullOrEmpty(Number)) { Set(IdentityState.Failed, "no_number"); yield break; }

                AuthStore.Save(AuthStore.Token, Number, AuthStore.LinkedSteamId);
                PhoneMod.Log.Msg($"Number restored: {Number}");
                Set(IdentityState.Ready, null);
            }
            finally { req.Dispose(); }
        }

        private static IEnumerator LinkFlow()
        {
            Set(IdentityState.Linking, null);

            string nonce = null, authUrl = null;

            var start = Post(Url("/v1/link/start"), "{}");
            try
            {
                yield return start.SendWebRequest();
                if (start.result != UnityWebRequest.Result.Success)
                {
                    PhoneMod.Log.Error($"link/start failed: {start.responseCode} {start.downloadHandler?.text}");
                    Set(IdentityState.Failed, "link_start_failed");
                    yield break;
                }
                var text = start.downloadHandler.text;
                nonce = MiniJson.GetString(text, "nonce");
                authUrl = MiniJson.GetString(text, "url");
            }
            finally { start.Dispose(); }

            if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(authUrl))
            {
                Set(IdentityState.Failed, "link_start_malformed");
                yield break;
            }

            PhoneMod.Log.Msg("Opening Steam login in browser...");
            Application.OpenURL(authUrl);

            var elapsed = 0f;
            while (elapsed < PollTimeout)
            {
                yield return new WaitForSeconds(PollInterval);
                elapsed += PollInterval;

                var poll = Post(Url("/v1/link/poll"), "{\"nonce\":\"" + MiniJson.Escape(nonce) + "\"}");
                var done = false;
                try
                {
                    yield return poll.SendWebRequest();
                    if (poll.result != UnityWebRequest.Result.Success) continue;

                    var text = poll.downloadHandler.text;
                    var status = MiniJson.GetString(text, "status");

                    if (status == "expired")
                    {
                        Set(IdentityState.NeedsLink, "link_expired");
                        done = true;
                    }
                    else if (status == "complete")
                    {
                        var token = MiniJson.GetString(text, "token");
                        Number = MiniJson.GetString(text, "number");
                        AuthStore.Save(token, Number, SteamIdentity.GetCurrent());
                        PhoneMod.Log.Msg($"Linked. Number: {Number}");
                        Set(IdentityState.Ready, null);
                        done = true;
                    }
                }
                finally { poll.Dispose(); }

                if (done) yield break;
            }

            Set(IdentityState.NeedsLink, "link_timeout");
        }

        private static string Url(string path) =>
            (ModConfig.WorkerUrl ?? "").TrimEnd('/') + path;

        private static UnityWebRequest Post(string url, string json)
        {
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;
            return req;
        }

        private static void Set(IdentityState state, string error)
        {
            State = state;
            LastError = error;
            if (error != null) PhoneMod.Log.Warning($"Identity -> {state} ({error})");
            try { OnChanged?.Invoke(); }
            catch (Exception e) { PhoneMod.Log.Error($"OnChanged handler threw: {e}"); }
        }
    }
}