using System;
using OperatorPhone.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OperatorPhone.UI
{
    /// <summary>
    /// The phone chrome: panel, status bar, body container.
    ///
    /// Built in code rather than from an AssetBundle. For M0 that's the right call —
    /// no bundle pipeline, no Unity editor round-trip, and the layout is simple enough.
    /// Revisit at M4 when media bubbles need real prefabs.
    /// </summary>
    public class PhoneShell : MonoBehaviour
    {
        private const float PanelW = 380f;
        private const float PanelH = 720f;
        public const float StatusH = 34f;
        private const float NavH = 40f;

        private static readonly Color Chrome = new Color(0.07f, 0.08f, 0.09f, 0.98f);
        private static readonly Color Bar = new Color(0.11f, 0.12f, 0.14f, 1f);
        private static readonly Color Dim = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color Accent = new Color(0.35f, 0.82f, 0.55f, 1f);

        private Canvas _canvas;
        private GameObject _panel;
        private Text _numberLabel;
        private Text _clockLabel;
        private Image _connDot;
        private GameObject _linkPanel;
        private Text _linkStatus;
        private Text _linkButtonLabel;
        private Font _font;
        private AppHost _host;
        private bool _open;

        public PhoneShell(IntPtr ptr) : base(ptr) { }

        private void Start()
        {
            _font = ResolveFont();
            Ui.Font = _font;
            Build();
            SetOpen(false);

            Identity.IdentityService.OnChanged += ApplyIdentity;
            ApplyIdentity();
        }

        /// <summary>
        /// Status bar reflects auth state directly. Until a number is claimed it shows
        /// the state, not a fake number — a placeholder that looks real would get shared
        /// and then not work.
        /// </summary>
        private void ApplyIdentity()
        {
            if (_numberLabel == null) return;

            var st = Identity.IdentityService.State;

            switch (st)
            {
                case Identity.IdentityState.Ready:
                    _numberLabel.text = Identity.IdentityService.Number;
                    _numberLabel.color = Color.white;
                    SetConnected(true);
                    break;
                case Identity.IdentityState.Linking:
                    _numberLabel.text = "linking...";
                    _numberLabel.color = Dim;
                    SetConnected(false);
                    break;
                case Identity.IdentityState.Authenticating:
                    _numberLabel.text = "connecting...";
                    _numberLabel.color = Dim;
                    SetConnected(false);
                    break;
                case Identity.IdentityState.NeedsLink:
                    _numberLabel.text = "not linked";
                    _numberLabel.color = Dim;
                    SetConnected(false);
                    break;
                case Identity.IdentityState.Failed:
                    _numberLabel.text = "no service";
                    _numberLabel.color = new Color(0.85f, 0.45f, 0.45f, 1f);
                    SetConnected(false);
                    break;
                default:
                    _numberLabel.text = "--- ----";
                    _numberLabel.color = Dim;
                    SetConnected(false);
                    break;
            }

            // The link prompt is the only interactive element until an account exists.
            if (_linkPanel != null)
                _linkPanel.SetActive(st == Identity.IdentityState.NeedsLink ||
                                     st == Identity.IdentityState.Linking ||
                                     st == Identity.IdentityState.Failed);

            if (_linkStatus != null)
            {
                _linkStatus.text = st switch
                {
                    Identity.IdentityState.Linking =>
                        "Waiting for Steam...\n\nComplete the login in your browser,\nthen return here.",
                    Identity.IdentityState.Failed =>
                        $"Couldn't reach the service.\n({Identity.IdentityService.LastError})",
                    _ =>
                        "This phone isn't linked yet.\n\nLinking opens Steam in your browser\nand assigns your number.",
                };
            }

            if (_linkButtonLabel != null)
                _linkButtonLabel.text = st == Identity.IdentityState.Linking ? "waiting..." : "LINK STEAM ACCOUNT";
        }

        /// <summary>
        /// Font resolution under IL2CPP, in order of reliability:
        ///
        /// 1. Steal one the game already loaded. Most dependable — OPERATOR has UI, so
        ///    it has Fonts in memory.
        /// 2. Builtin resource. Renamed "Arial.ttf" -> "LegacyRuntime.ttf" in Unity 2022+,
        ///    so try both.
        /// 3. OS fonts. Frequently returns null under IL2CPP even when fonts exist,
        ///    which is why it's last rather than first.
        /// </summary>
        private static Font ResolveFont()
        {
            try
            {
                var loaded = Resources.FindObjectsOfTypeAll<Font>();
                if (loaded != null && loaded.Length > 0)
                {
                    // Prefer something monospace-ish for tabular figures in the number.
                    foreach (var f in loaded)
                    {
                        if (f == null) continue;
                        var n = f.name ?? "";
                        if (n.IndexOf("mono", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("consol", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("courier", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            PhoneMod.Log.Msg($"Font: borrowed '{n}' from game assets (mono).");
                            return f;
                        }
                    }

                    foreach (var f in loaded)
                    {
                        if (f == null) continue;
                        PhoneMod.Log.Msg($"Font: borrowed '{f.name}' from game assets.");
                        return f;
                    }
                }
            }
            catch (Exception e)
            {
                PhoneMod.Log.Warning($"Font asset scan failed: {e.Message}");
            }

            foreach (var builtin in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var f = Resources.GetBuiltinResource<Font>(builtin);
                    if (f != null)
                    {
                        PhoneMod.Log.Msg($"Font: builtin '{builtin}'.");
                        return f;
                    }
                }
                catch { }
            }

            foreach (var name in new[] { "Consolas", "Cascadia Mono", "Courier New", "Segoe UI", "Arial" })
            {
                try
                {
                    var f = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (f != null)
                    {
                        PhoneMod.Log.Msg($"Font: OS font '{name}'.");
                        return f;
                    }
                }
                catch { }
            }

            PhoneMod.Log.Error("No usable font found — labels will be invisible.");
            return null;
        }

        /// <summary>
        /// Start() can run before the game's UI assets are loaded, in which case the
        /// asset scan finds nothing. Retry on first open and repair existing labels.
        /// </summary>
        private void TryLateFont()
        {
            if (_font != null) return;

            _font = ResolveFont();
            if (_font == null) return;
            Ui.Font = _font;

            foreach (var t in GetComponentsInChildren<Text>(true))
                t.font = _font;

            PhoneMod.Log.Msg("Font resolved late — labels repaired.");
        }

        private void Build()
        {
            var canvasGo = new GameObject("PhoneCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30000; // above the game's HUD

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            _panel = MakeRect("Panel", canvasGo.transform, Chrome);
            var pr = _panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(1f, 0.5f);
            pr.pivot = new Vector2(1f, 0.5f);
            pr.sizeDelta = new Vector2(PanelW, PanelH) * ModConfig.UiScale;
            pr.anchoredPosition = new Vector2(-40f, 0f);

            BuildStatusBar(_panel.transform);
            BuildBody(_panel.transform);
            BuildLinkPanel(_panel.transform);
        }

        private void BuildStatusBar(Transform parent)
        {
            var bar = MakeRect("StatusBar", parent, Bar);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 1f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 1f);
            br.sizeDelta = new Vector2(0f, StatusH);
            br.anchoredPosition = Vector2.zero;

            // Own number, left. This is load-bearing: with no Steam-based discovery,
            // the status bar is how players share their number at all.
            _numberLabel = MakeText("Number", bar.transform, ModConfig.DebugNumber,
                16, TextAnchor.MiddleLeft, Color.white);
            var nr = _numberLabel.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f);
            nr.anchorMax = new Vector2(0.6f, 1f);
            nr.offsetMin = new Vector2(30f, 0f);
            nr.offsetMax = Vector2.zero;

            // Connection dot, sits just left of the number.
            var dot = MakeRect("ConnDot", bar.transform, Dim);
            _connDot = dot.GetComponent<Image>();
            var dr = dot.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0f, 0.5f);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(8f, 8f);
            dr.anchoredPosition = new Vector2(16f, 0f);

            _clockLabel = MakeText("Clock", bar.transform, "--:--",
                14, TextAnchor.MiddleRight, Dim);
            var cr = _clockLabel.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.6f, 0f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = new Vector2(-14f, 0f);
        }

        private void BuildBody(Transform parent)
        {
            _host = new AppHost();
            _host.Build(parent, NavH);
            var appArea = _host.BuildAppArea(parent, NavH);

            // Home registers LAST so its grid can see every other app, but is flagged
            // as home so the host opens it first.
            _host.Register(new Apps.MessagesApp(), appArea);
            _host.Register(new Apps.ContactsApp(), appArea);
            _host.Register(new Apps.SettingsApp(), appArea);
            _host.Register(new Apps.HomeApp(), appArea, isHome: true);

            _host.ShowHome();
        }

        /// <summary>
        /// Overlays the body until an account exists. Full-cover rather than a small
        /// banner: there is nothing else usable on an unlinked phone, so a dismissible
        /// prompt would just leave people staring at an empty app.
        /// </summary>
        private void BuildLinkPanel(Transform parent)
        {
            _linkPanel = MakeRect("LinkPanel", parent, new Color(0.09f, 0.10f, 0.11f, 1f));
            var rt = _linkPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -StatusH);

            _linkStatus = MakeText("LinkStatus", _linkPanel.transform, "", 14,
                TextAnchor.MiddleCenter, Dim);
            var sr = _linkStatus.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 0.45f);
            sr.anchorMax = new Vector2(1f, 0.75f);
            sr.offsetMin = new Vector2(20f, 0f);
            sr.offsetMax = new Vector2(-20f, 0f);
            _linkStatus.horizontalOverflow = HorizontalWrapMode.Wrap;

            var btn = MakeRect("LinkButton", _linkPanel.transform, new Color(0.16f, 0.34f, 0.24f, 1f));
            var br = btn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.5f, 0.36f);
            br.anchorMax = new Vector2(0.5f, 0.36f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(240f, 44f);
            br.anchoredPosition = Vector2.zero;

            _linkButtonLabel = MakeText("LinkButtonLabel", btn.transform, "LINK STEAM ACCOUNT",
                13, TextAnchor.MiddleCenter, Color.white);
            var lr = _linkButtonLabel.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            var button = btn.AddComponent<Button>();
            button.targetGraphic = btn.GetComponent<Image>();
            button.onClick.AddListener((UnityEngine.Events.UnityAction)OnLinkClicked);
        }

        private void OnLinkClicked()
        {
            if (Identity.IdentityService.State == Identity.IdentityState.Linking) return;
            Identity.IdentityService.StartLink();
        }

        private static void EnsureEventSystem()
        {
            // Creating a second EventSystem breaks the game's own UI — Unity logs
            // "Multiple EventSystems in scene" and input routing goes nondeterministic.
            if (EventSystem.current != null) return;

            var go = new GameObject("PhoneEventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            PhoneMod.Log.Warning("No EventSystem found — created one. Watch for UI conflicts.");
        }

        private static GameObject MakeRect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private Text MakeText(string name, Transform parent, string content,
            int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = content;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public void Toggle() => SetOpen(!_open);

        private void SetOpen(bool open)
        {
            _open = open;
            if (_canvas != null) _canvas.enabled = open; // zero cost when closed

            if (open) TryLateFont();

            var gate = InputGate.Instance;
            if (gate == null) return;

            if (open) gate.Capture();
            else gate.Release();
        }

        private void Update()
        {
            if (!_open) return;

            var now = DateTime.Now;
            if (_clockLabel != null) _clockLabel.text = now.ToString("HH:mm");

        }

        private void OnDestroy()
        {
            Identity.IdentityService.OnChanged -= ApplyIdentity;
        }

        public void SetNumber(string number)
        {
            if (_numberLabel != null) _numberLabel.text = number;
        }

        public void SetConnected(bool connected)
        {
            if (_connDot != null) _connDot.color = connected ? Accent : Dim;
        }
    }
}