using System;
using OperatorPhone.Chat;
using OperatorPhone.Data;
using OperatorPhone.Identity;
using OperatorPhone.Net;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace OperatorPhone.UI.Apps
{
    /// <summary>
    /// Two views in one app: the thread list and a single open conversation. New
    /// conversations start by number; the number resolves to an account id through the
    /// Worker's /v1/lookup, then everything runs on account ids (numbers are the human
    /// layer, ids are the routing layer — see spec §4).
    /// </summary>
    internal class MessagesApp : PhoneApp
    {
        public override string Title =>
            _openThread == null ? "Messages" : (PeerLabel(_openThread));
        public override string Glyph => "M";

        private GameObject _listView;
        private RectTransform _listContent;
        private GameObject _convoView;
        private RectTransform _convoContent;
        private ScrollRect _convoScroll;
        private InputField _composer;
        private InputField _newNumber;
        private Text _newStatus;
        private Data.Thread _openThread;

        protected override void Build(Transform root)
        {
            BuildListView(root);
            BuildConvoView(root);
            ShowList();

            MessageStore.OnThread += _ =>
            {
                // Rebuild whichever view is showing. Coarse, but fine at M2 volumes;
                // virtualization comes with media in M4 when it starts to matter.
                if (_openThread != null) RefreshConvo();
                else RefreshList();
            };
        }

        /* -------------------------------------------------------- thread list */

        private void BuildListView(Transform root)
        {
            _listView = Ui.Invisible("ListView", root);
            Ui.Fill(_listView.GetComponent<RectTransform>());

            // New-conversation row pinned to the top.
            var newRow = Ui.Rect("NewRow", _listView.transform, Ui.Panel);
            var nr = newRow.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 1f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(0f, 74f);

            _newNumber = Ui.TextInput("NewNumber", newRow.transform, "201-4417");
            var inr = _newNumber.GetComponent<RectTransform>();
            inr.anchorMin = new Vector2(0f, 1f);
            inr.anchorMax = new Vector2(1f, 1f);
            inr.pivot = new Vector2(0.5f, 1f);
            inr.sizeDelta = new Vector2(-96f, 30f);
            inr.anchoredPosition = new Vector2(-40f, -6f);

            var startBtn = Ui.TextButton("Start", newRow.transform, "NEW",
                Ui.AccentDark, Color.white, 12, StartConversation);
            var sr = startBtn.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(1f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(1f, 1f);
            sr.sizeDelta = new Vector2(64f, 30f);
            sr.anchoredPosition = new Vector2(-8f, -6f);

            _newStatus = Ui.Label("NewStatus", newRow.transform, "", 11,
                TextAnchor.MiddleLeft, Ui.Dim);
            var str_ = _newStatus.GetComponent<RectTransform>();
            str_.anchorMin = new Vector2(0f, 0f);
            str_.anchorMax = new Vector2(1f, 0f);
            str_.pivot = new Vector2(0.5f, 0f);
            str_.sizeDelta = new Vector2(-16f, 26f);
            str_.anchoredPosition = new Vector2(0f, 4f);

            // Thread list fills the rest.
            _listContent = Ui.ScrollList("Threads", _listView.transform, out var scroll);
            var vr = scroll.GetComponent<RectTransform>();
            Ui.Fill(vr);
            vr.offsetMax = new Vector2(0f, -74f);
        }

        private void RefreshList()
        {
            Clear(_listContent);

            var any = false;
            foreach (var t in MessageStore.Threads)
            {
                any = true;
                AddThreadRow(t);
            }

            if (!any)
            {
                var empty = Ui.Label("Empty", _listContent, "No conversations yet.\nStart one with a number above.",
                    13, TextAnchor.MiddleCenter, Ui.Dim, wrap: true);
                Ui.FixedHeight(empty.gameObject, 80f);
            }
        }

        private void AddThreadRow(Data.Thread t)
        {
            var row = Ui.Rect("Thread", _listContent, Ui.Panel);
            Ui.FixedHeight(row, 58f);

            var name = Ui.Label("Peer", row.transform,
                PeerLabel(t) + (t.Unread > 0 ? $"  ({t.Unread})" : ""),
                14, TextAnchor.UpperLeft, t.Unread > 0 ? Ui.Accent : Ui.Text);
            var nr = name.GetComponent<RectTransform>();
            Ui.Fill(nr);
            nr.offsetMin = new Vector2(12f, 26f);
            nr.offsetMax = new Vector2(-12f, -6f);

            var preview = t.Last?.Body ?? "";
            if (preview.Length > 46) preview = preview.Substring(0, 46) + "...";
            var prev = Ui.Label("Preview", row.transform, preview, 12,
                TextAnchor.UpperLeft, Ui.Dim);
            var pr = prev.GetComponent<RectTransform>();
            Ui.Fill(pr);
            pr.offsetMin = new Vector2(12f, 6f);
            pr.offsetMax = new Vector2(-12f, -28f);

            var btn = row.AddComponent<Button>();
            btn.targetGraphic = row.GetComponent<Image>();
            var captured = t;
            btn.onClick.AddListener((UnityEngine.Events.UnityAction)(() => OpenThread(captured)));
        }

        /* ------------------------------------------------------- conversation */

        private void BuildConvoView(Transform root)
        {
            _convoView = Ui.Invisible("ConvoView", root);
            Ui.Fill(_convoView.GetComponent<RectTransform>());

            _convoContent = Ui.ScrollList("Bubbles", _convoView.transform, out _convoScroll);
            var vr = _convoScroll.GetComponent<RectTransform>();
            Ui.Fill(vr);
            vr.offsetMin = new Vector2(0f, 46f);

            var composeRow = Ui.Rect("ComposeRow", _convoView.transform, Ui.Panel);
            var cr = composeRow.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 0f);
            cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(0.5f, 0f);
            cr.sizeDelta = new Vector2(0f, 46f);

            _composer = Ui.TextInput("Composer", composeRow.transform, "message");
            var mr = _composer.GetComponent<RectTransform>();
            Ui.Fill(mr);
            mr.offsetMin = new Vector2(8f, 8f);
            mr.offsetMax = new Vector2(-72f, -8f);

            var send = Ui.TextButton("Send", composeRow.transform, "SEND",
                Ui.AccentDark, Color.white, 12, SendCurrent);
            var sr = send.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(1f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(1f, 0.5f);
            sr.sizeDelta = new Vector2(60f, -16f);
            sr.anchoredPosition = new Vector2(-8f, 0f);
        }

        private void OpenThread(Data.Thread t)
        {
            _openThread = t;
            MessageStore.MarkRead(t);
            _listView.SetActive(false);
            _convoView.SetActive(true);
            RefreshConvo();
        }

        private void ShowList()
        {
            _openThread = null;
            _convoView.SetActive(false);
            _listView.SetActive(true);
            RefreshList();
        }

        private void RefreshConvo()
        {
            if (_openThread == null) return;
            Clear(_convoContent);

            foreach (var m in _openThread.Messages)
                AddBubble(m);

            // Snap to newest after layout settles.
            MelonCoroutines.Start(ScrollToBottomNextFrame());
        }

        private System.Collections.IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            if (_convoScroll != null) _convoScroll.verticalNormalizedPosition = 0f;
        }

        // Widest a bubble may get. The phone panel is 380 units wide with 8 units of
        // list padding each side, so this leaves a clear gutter on the opposite side —
        // which is what makes the left/right alignment readable at a glance.
        private const float MaxBubbleW = 226f;
        private const float BubblePadX = 14f;
        private const float BubblePadY = 9f;

        private void AddBubble(Message m)
        {
            var row = Ui.Invisible("Row", _convoContent);
            var le = row.AddComponent<LayoutElement>();

            var bubble = Ui.Rect("Bubble", row.transform, m.Mine ? Ui.BubbleMine : Ui.BubbleTheirs);
            var br = bubble.GetComponent<RectTransform>();

            var label = Ui.Label("Body", bubble.transform, m.Body, 13,
                TextAnchor.UpperLeft, Ui.Text, wrap: true);
            var lr = label.GetComponent<RectTransform>();

            // Measure at the maximum text width, then shrink the bubble to fit short
            // messages. Sizing to a fixed width instead made every bubble the same
            // width and pushed them past the row edges.
            var textMax = MaxBubbleW - BubblePadX * 2f;
            var gen = new TextGenerator();
            var settings = label.GetGenerationSettings(new Vector2(textMax, 0f));

            var textW = Mathf.Min(gen.GetPreferredWidth(m.Body, settings) / label.pixelsPerUnit, textMax);
            var textH = gen.GetPreferredHeight(m.Body, settings) / label.pixelsPerUnit;

            var bubbleW = Mathf.Clamp(textW + BubblePadX * 2f, 44f, MaxBubbleW);
            var bubbleH = Mathf.Max(30f, textH + BubblePadY * 2f);

            le.preferredHeight = bubbleH + 4f;
            le.minHeight = bubbleH + 4f;

            // Anchor to one edge of the row and size explicitly. Row width is driven by
            // the layout group, so anchoring to a corner keeps the bubble inside it
            // regardless of how wide the panel ends up.
            br.anchorMin = new Vector2(m.Mine ? 1f : 0f, 0.5f);
            br.anchorMax = br.anchorMin;
            br.pivot = new Vector2(m.Mine ? 1f : 0f, 0.5f);
            br.sizeDelta = new Vector2(bubbleW, bubbleH);
            br.anchoredPosition = Vector2.zero;

            // Label fills the bubble minus padding, so wrapping matches the measurement.
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(BubblePadX, BubblePadY);
            lr.offsetMax = new Vector2(-BubblePadX, -BubblePadY);
        }

        /* ------------------------------------------------------------- actions */

        private void StartConversation()
        {
            var number = (_newNumber.text ?? "").Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(number, @"^[2-9]\d{2}-\d{4}$"))
            {
                _newStatus.text = "format: 201-4417";
                _newStatus.color = Ui.Danger;
                return;
            }
            if (number == IdentityService.Number)
            {
                _newStatus.text = "that's your own number";
                _newStatus.color = Ui.Danger;
                return;
            }

            var known = ContactStore.Find(number);
            if (known?.AccountId != null)
            {
                OpenThread(MessageStore.GetOrCreate(known.AccountId, number));
                Host.Push(this); // refresh title
                return;
            }

            _newStatus.text = "looking up...";
            _newStatus.color = Ui.Dim;
            MelonCoroutines.Start(Lookup(number));
        }

        private System.Collections.IEnumerator Lookup(string number)
        {
            var url = ServiceConfig.WorkerUrl + "/v1/lookup?number=" +
                      UnityWebRequest.EscapeURL(number);
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", "Bearer " + AuthStore.Token);
            req.timeout = 10;
            try
            {
                yield return req.SendWebRequest();
                var text = req.downloadHandler?.text;

                if (req.result != UnityWebRequest.Result.Success)
                {
                    _newStatus.text = "lookup failed (" +
                        (MiniJson.GetString(text, "error") ?? req.responseCode.ToString()) + ")";
                    _newStatus.color = Ui.Danger;
                    yield break;
                }

                if (!MiniJson.GetBool(text, "found"))
                {
                    _newStatus.text = "no phone at that number";
                    _newStatus.color = Ui.Danger;
                    yield break;
                }

                var accountId = MiniJson.GetString(text, "id");
                ContactStore.AddOrUpdate(number, null, accountId);
                _newStatus.text = "";
                OpenThread(MessageStore.GetOrCreate(accountId, number));
            }
            finally { req.Dispose(); }
        }

        private void SendCurrent()
        {
            if (_openThread == null) return;
            var body = (_composer.text ?? "").Trim();
            if (body.Length == 0) return;

            var svc = ChatService.Instance;
            if (svc == null || !svc.IsConnected)
            {
                // Local-echo-only would create the illusion of delivery. Refuse instead;
                // M3's outbox queue is the real fix for sending while offline.
                PhoneMod.Log.Warning("Not connected — message not sent.");
                return;
            }

            var m = MessageStore.AppendMine(_openThread, body);
            var envelope = MessageStore.BuildTextEnvelope(m.Id, body);

            if (!svc.Send(_openThread.PeerAccountId, envelope))
                PhoneMod.Log.Error("Send failed after append — will appear locally only.");

            _composer.text = "";
            _composer.ActivateInputField();
        }

        /* --------------------------------------------------------------- misc */

        public override void OnOpened()
        {
            if (_openThread == null) RefreshList();
        }

        public override void OnClosed()
        {
            // Leaving the app closes any open conversation, so Back from the thread
            // list exits to home rather than bouncing through the conversation.
            if (_openThread != null) ShowList();
        }

        private static string PeerLabel(Data.Thread t)
        {
            var c = ContactStore.FindByAccount(t.PeerAccountId);
            if (c != null && !string.IsNullOrEmpty(c.Name)) return c.Name;
            return t.PeerNumber ?? "unknown";
        }

        private static void Clear(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
