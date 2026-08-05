using System;
using System.Collections.Generic;
using Photon.Client;
using OperatorPhone.Identity;
using Photon.Chat;

namespace OperatorPhone.Chat
{
    /// <summary>
    /// Photon Chat transport.
    ///
    /// Deliberately NOT a MonoBehaviour: the Photon SDK is a plain managed assembly, so
    /// this needs no Il2Cpp registration. It's pumped from PhoneMod.OnUpdate instead.
    ///
    /// Chat is transport only — it does not durably store private message history.
    /// Scrollback and offline delivery are M3's problem (local log + server inbox);
    /// don't be tempted to rely on Photon for either.
    /// </summary>
    internal class ChatService : IChatClientListener
    {

        public static ChatService Instance { get; private set; }

        public bool IsConnected { get; private set; }
        public string LastError { get; private set; }

        public event Action OnStateChanged;
        /// <summary>senderId, raw envelope JSON.</summary>
        public event Action<string, string> OnMessage;

        private ChatClient _client;
        private readonly HashSet<string> _friends = new HashSet<string>();

        public static void Start()
        {
            if (Instance != null) return;
            Instance = new ChatService();
            Instance.Connect();
        }

        public static void Stop()
        {
            Instance?.Disconnect();
            Instance = null;
        }

        private void Connect()
        {
            var appId = ModConfig.PhotonChatAppId;
            if (string.IsNullOrEmpty(appId) || appId == "REPLACE_ME")
            {
                LastError = "no_app_id";
                PhoneMod.Log.Error("Photon Chat App ID not set — see MelonPreferences.cfg.");
                return;
            }

            if (!AuthStore.HasToken)
            {
                LastError = "not_linked";
                return;
            }

            _client = new ChatClient(this);

            // Custom auth: Photon calls our Worker to validate this token before it will
            // assign a UserId. Without it, a client could connect claiming any identity.
            var auth = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            auth.AddAuthParameter("token", AuthStore.Token);
            _client.AuthValues = auth;

            // SDK v5: Connect(appId, version, authValues) still exists but is [Obsolete].
            // ConnectUsingSettings is the supported path and carries region/server config
            // we'll want later.
            var settings = new ChatAppSettings
            {
                AppIdChat = appId,
                AppVersion = ServiceConfig.PhotonAppVersion,
            };

            PhoneMod.Log.Msg("Connecting to Photon Chat...");
            _client.ConnectUsingSettings(settings);
        }

        private void Disconnect()
        {
            try { _client?.Disconnect(); } catch { }
            _client = null;
            IsConnected = false;
        }

        /// <summary>
        /// Must be called every frame. Photon's .NET client is not threaded — it only
        /// sends, receives, and dispatches callbacks inside Service().
        /// </summary>
        public void Pump()
        {
            try { _client?.Service(); }
            catch (Exception e) { PhoneMod.Log.Error($"Photon Service() threw: {e.Message}"); }
        }

        /// <summary>Sends a raw envelope to a recipient's account id.</summary>
        public bool Send(string recipientId, string envelopeJson)
        {
            if (!IsConnected || _client == null) return false;
            try
            {
                return _client.SendPrivateMessage(recipientId, envelopeJson);
            }
            catch (Exception e)
            {
                PhoneMod.Log.Error($"SendPrivateMessage failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Subscribes to presence for contacts. Photon provides this natively.</summary>
        public void WatchContacts(IEnumerable<string> accountIds)
        {
            if (!IsConnected || _client == null) return;

            var added = new List<string>();
            foreach (var id in accountIds)
                if (!string.IsNullOrEmpty(id) && _friends.Add(id)) added.Add(id);

            if (added.Count == 0) return;
            try { _client.AddFriends(added.ToArray()); }
            catch (Exception e) { PhoneMod.Log.Error($"AddFriends failed: {e.Message}"); }
        }

        public void SetStatus(int status)
        {
            if (!IsConnected || _client == null) return;
            try { _client.SetOnlineStatus(status); }
            catch (Exception e) { PhoneMod.Log.Error($"SetOnlineStatus failed: {e.Message}"); }
        }

        /* ------------------------------------------------ IChatClientListener */

        public void OnConnected()
        {
            IsConnected = true;
            // Route incoming envelopes into the store exactly once per service instance.
            OnMessage -= Data_Route;
            OnMessage += Data_Route;
            LastError = null;
            PhoneMod.Log.Msg("Photon Chat connected.");
            SetStatus(ChatUserStatus.Online);
            OnStateChanged?.Invoke();
        }

        private static void Data_Route(string sender, string envelope) =>
            OperatorPhone.Data.MessageStore.AppendTheirs(sender, envelope);

        public void OnDisconnected()
        {
            IsConnected = false;
            PhoneMod.Log.Warning("Photon Chat disconnected.");
            OnStateChanged?.Invoke();
        }

        public void OnChatStateChange(ChatState state)
        {
            PhoneMod.Log.Msg($"Photon Chat state: {state}");
        }

        public void OnPrivateMessage(string sender, object message, string channelName)
        {
            var text = message?.ToString();
            if (string.IsNullOrEmpty(text)) return;

            // Echo of our own outgoing message — Photon delivers both sides of a private
            // channel. Ignore it; the local copy was already written on send.
            if (sender == AuthStore.AccountId) return;

            try { OnMessage?.Invoke(sender, text); }
            catch (Exception e) { PhoneMod.Log.Error($"Message handler threw: {e}"); }
        }

        public void OnGetMessages(string channelName, string[] senders, object[] messages)
        {
            // Public channels are unused for now; 1:1 goes through OnPrivateMessage and
            // group threads (if they land) will use channels.
        }

        public void OnSubscribed(string[] channels, bool[] results) { }
        public void OnUnsubscribed(string[] channels) { }
        public void OnUserSubscribed(string channel, string user) { }
        public void OnUserUnsubscribed(string channel, string user) { }

        public void OnStatusUpdate(string user, int status, bool gotMessage, object message)
        {
            OnStateChanged?.Invoke();
        }

        public void DebugReturn(LogLevel level, string message)
        {
            if (level == LogLevel.Error) PhoneMod.Log.Error($"[Photon] {message}");
            else if (level == LogLevel.Warning) PhoneMod.Log.Warning($"[Photon] {message}");
        }

        /// <summary>
        /// Fired when our Worker's /v1/photon/auth returned ResultCode 1. The UserId
        /// Photon assigned is the account hash we vouched for — stash it so we can
        /// recognise our own echoed messages.
        /// </summary>
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            var id = _client?.AuthValues?.UserId;
            if (!string.IsNullOrEmpty(id))
            {
                AuthStore.AccountId = id;
                PhoneMod.Log.Msg($"Custom auth OK (UserId {id.Substring(0, Math.Min(8, id.Length))}...).");
            }
        }

        /// <summary>
        /// Worker rejected the token, or Photon couldn't reach it. Distinguish this from
        /// a network failure in the UI: the fix is relinking, not retrying.
        /// </summary>
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            IsConnected = false;
            LastError = "auth_failed";
            PhoneMod.Log.Error($"Photon custom auth failed: {debugMessage}");
            OnStateChanged?.Invoke();
        }
    }
}