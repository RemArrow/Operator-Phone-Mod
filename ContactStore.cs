using System;
using System.Collections.Generic;
using System.IO;
using OperatorPhone.Net;
using UnityEngine;

namespace OperatorPhone.Data
{
    internal class Contact
    {
        public string Number;     // AAA-NNNN
        public string Name;       // user-chosen nickname
        public string AccountId;  // opaque hash from /v1/lookup; resolved lazily
    }

    /// <summary>
    /// Contacts, persisted as JSON-lines under UserData. JSONL rather than one JSON
    /// array so a torn write at worst loses the final line instead of the whole file.
    /// </summary>
    internal static class ContactStore
    {
        private static readonly List<Contact> _contacts = new List<Contact>();
        public static IReadOnlyList<Contact> All => _contacts;

        public static event Action OnChanged;

        public static void Load()
        {
            _contacts.Clear();
            try
            {
                var path = FilePath();
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var c = new Contact
                    {
                        Number = MiniJson.GetString(line, "n"),
                        Name = MiniJson.GetString(line, "d"),
                        AccountId = MiniJson.GetString(line, "a"),
                    };
                    if (!string.IsNullOrEmpty(c.Number)) _contacts.Add(c);
                }
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"Contact load failed: {e.Message}");
            }
        }

        public static Contact Find(string number)
        {
            foreach (var c in _contacts)
                if (c.Number == number) return c;
            return null;
        }

        public static Contact FindByAccount(string accountId)
        {
            if (string.IsNullOrEmpty(accountId)) return null;
            foreach (var c in _contacts)
                if (c.AccountId == accountId) return c;
            return null;
        }

        public static void AddOrUpdate(string number, string name, string accountId = null)
        {
            var existing = Find(number);
            if (existing != null)
            {
                if (!string.IsNullOrEmpty(name)) existing.Name = name;
                if (!string.IsNullOrEmpty(accountId)) existing.AccountId = accountId;
            }
            else
            {
                _contacts.Add(new Contact { Number = number, Name = name, AccountId = accountId });
            }
            Save();
            OnChanged?.Invoke();
        }

        public static void Remove(string number)
        {
            _contacts.RemoveAll(c => c.Number == number);
            Save();
            OnChanged?.Invoke();
        }

        private static void Save()
        {
            try
            {
                var path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var lines = new List<string>(_contacts.Count);
                foreach (var c in _contacts)
                {
                    lines.Add("{\"n\":\"" + MiniJson.Escape(c.Number) +
                              "\",\"d\":\"" + MiniJson.Escape(c.Name ?? "") +
                              "\",\"a\":\"" + MiniJson.Escape(c.AccountId ?? "") + "\"}");
                }
                File.WriteAllLines(path, lines);
            }
            catch (Exception e)
            {
                PhoneMod.Log.Error($"Contact save failed: {e.Message}");
            }
        }

        private static string FilePath()
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName
                       ?? Directory.GetCurrentDirectory();
            return Path.Combine(root, "UserData", "OperatorPhone", "contacts.jsonl");
        }
    }
}
