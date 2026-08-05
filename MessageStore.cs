using System;
using System.Collections.Generic;
using System.IO;
using OperatorPhone.Net;
using UnityEngine;

namespace OperatorPhone.Data
{
    internal class Message
    {
        public string Id;         // ULID-ish, client generated
        public long Ts;           // unix seconds, sender clock
        public string Body;
        public bool Mine;
    }

    internal class Thread
    {
        public string PeerAccountId;
        public string PeerNumber;   // may be null until resolved
        public readonly List<Message> Messages = new List<Message>();
        public int Unread;

        public Message Last => Messages.Count > 0 ? Messages[Messages.Count - 1] : null;
    }

    /// <summary>
    /// Message history. Threads keyed by peer account id; each thread appends to its own
    /// JSONL file under UserData (§6 of the spec: local log is the source of truth for
    /// scrollback — Photon is transport only and stores nothing).
    ///
    /// M2 scope: local persistence + live messages. The offline inbox drain is M3.
    /// </summary>
    internal static class MessageStore
    {
        private static readonly Dictionary<string, Thread> _threads =
            new Dictionary<string, Thread>();

        /// <summary>Fired with the affected thread when anything changes.</summary>
        public static event Action<Thread> OnThread;

        public static IEnumerable<Thread> Threads
        {
            get
            {
                // Most-recent-first, the order every messaging app lists threads in.
                var list = new List<Thread>(_threads.Values);
                list.Sort((a, b) => (b.Last?.Ts ?? 0).CompareTo(a.Last?.Ts ?? 0));
                return list;
            }
        }

        public static Thread GetOrCreate(string peerAccountId, string peerNumber = null)
        {
            if (!_threads.TryGetValue(peerAccountId, out var t))
            {
                t = new Thread { PeerAccountId = peerAccountId, PeerNumber = peerNumber };
                _threads[peerAccountId] = t;
                LoadThread(t);
            }
            if (peerNumber != null && t.PeerNumber == null) t.PeerNumber = peerNumber;
            return t;
        }

        /// <summary>Records an outgoing message locally. Transport is the caller's job.</summary>
        public static Message AppendMine(Thread t, string body)
        {
            var m = new Message
            {
                Id = NewId(),
                Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = body,
                Mine = true,
            };
            t.Messages.Add(m);
            Persist(t, m);
            OnThread?.Invoke(t);
            return m;
        }

        /// <summary>Handles an incoming envelope. Dedupes on id (§6: merge is idempotent).</summary>
        public static void AppendTheirs(string senderAccountId, string envelopeJson)
        {
            var type = MiniJson.GetString(envelopeJson, "t");
            if (type != "txt")
            {
                // Unknown types render as nothing for now; M4+ adds img/lnk. Do not
                // throw — a newer client's message must not break an older one (§5).
                PhoneMod.Log.Msg($"Ignoring envelope type '{type ?? "?"}'.");
                return;
            }

            var id = MiniJson.GetString(envelopeJson, "id");
            var body = MiniJson.GetString(envelopeJson, "b");
            if (string.IsNullOrEmpty(body)) return;
            if (body.Length > 2000) body = body.Substring(0, 2000); // clamp hostile input

            var t = GetOrCreate(senderAccountId);

            if (!string.IsNullOrEmpty(id))
                foreach (var existing in t.Messages)
                    if (existing.Id == id) return; // duplicate delivery

            var m = new Message
            {
                Id = id ?? NewId(),
                Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Body = body,
                Mine = false,
            };
            t.Messages.Add(m);
            t.Unread++;
            Persist(t, m);
            OnThread?.Invoke(t);
        }

        public static string BuildTextEnvelope(string id, string body) =>
            "{\"v\":1,\"t\":\"txt\",\"id\":\"" + MiniJson.Escape(id) +
            "\",\"ts\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() +
            ",\"b\":\"" + MiniJson.Escape(body) + "\"}";

        public static void MarkRead(Thread t)
        {
            if (t.Unread == 0) return;
            t.Unread = 0;
            OnThread?.Invoke(t);
        }

        public static int TotalUnread
        {
            get
            {
                var n = 0;
                foreach (var t in _threads.Values) n += t.Unread;
                return n;
            }
        }

        /* ------------------------------------------------------------ disk */

        private static void LoadThread(Thread t)
        {
            try
            {
                var path = ThreadPath(t.PeerAccountId);
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var m = new Message
                    {
                        Id = MiniJson.GetString(line, "id"),
                        Body = MiniJson.GetString(line, "b"),
                        Mine = MiniJson.GetBool(line, "m"),
                    };
                    long.TryParse(MiniJson.GetString(line, "ts") ?? "0", out m.Ts);
                    if (!string.IsNullOrEmpty(m.Body)) t.Messages.Add(m);
                }
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"History load failed for thread: {e.Message}");
            }
        }

        private static void Persist(Thread t, Message m)
        {
            try
            {
                var path = ThreadPath(t.PeerAccountId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var line = "{\"id\":\"" + MiniJson.Escape(m.Id) +
                           "\",\"ts\":\"" + m.Ts +
                           "\",\"m\":" + (m.Mine ? "true" : "false") +
                           ",\"b\":\"" + MiniJson.Escape(m.Body) + "\"}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                PhoneMod.Log.Error($"History persist failed: {e.Message}");
            }
        }

        private static string ThreadPath(string peerAccountId)
        {
            // Account ids are hex, so they're filesystem-safe as-is; truncate for sanity.
            var safe = peerAccountId.Length > 32 ? peerAccountId.Substring(0, 32) : peerAccountId;
            var root = Directory.GetParent(Application.dataPath)?.FullName
                       ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "UserData", "OperatorPhone", "history", safe + ".jsonl");
        }

        private static readonly System.Random _rng = new System.Random();

        private static string NewId()
        {
            // Timestamp prefix + randomness: sortable like a ULID without the dependency.
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString("x12");
            var rand = new byte[5];
            lock (_rng) _rng.NextBytes(rand);
            var sb = new System.Text.StringBuilder(ts, 22);
            foreach (var b in rand) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
