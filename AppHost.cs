using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OperatorPhone.UI
{
    /// <summary>
    /// One phone app: owns a root GameObject inside the app area, built lazily on first
    /// open. Plain classes rather than MonoBehaviours — nothing here needs Unity
    /// lifecycle, and every non-MonoBehaviour is one less Il2Cpp registration.
    /// </summary>
    internal abstract class PhoneApp
    {
        public abstract string Title { get; }
        /// <summary>Short glyph for the home grid. Text, not icons — no asset pipeline yet.</summary>
        public abstract string Glyph { get; }

        public GameObject Root { get; private set; }
        protected AppHost Host { get; private set; }

        public void Initialize(AppHost host, Transform appArea)
        {
            Host = host;
            Root = Ui.Invisible("App_" + Title, appArea);
            Ui.Fill(Root.GetComponent<RectTransform>());
            Build(Root.transform);
            Root.SetActive(false);
        }

        protected abstract void Build(Transform root);

        public virtual void OnOpened() { }
        public virtual void OnClosed() { }
    }

    /// <summary>
    /// Owns the app area, the nav bar, and a back stack. Kept deliberately simple:
    /// no transitions, no history persistence — apps are singletons that show/hide.
    /// </summary>
    internal class AppHost
    {
        private readonly List<PhoneApp> _apps = new List<PhoneApp>();
        private readonly Stack<PhoneApp> _stack = new Stack<PhoneApp>();
        private Text _titleLabel;
        private GameObject _backButton;
        private GameObject _homeButton;
        private PhoneApp _home;

        public IReadOnlyList<PhoneApp> Apps => _apps;
        public PhoneApp Current => _stack.Count > 0 ? _stack.Peek() : null;

        public void Build(Transform parent, float navHeight)
        {
            // Nav bar sits directly under the status bar.
            var nav = Ui.Rect("NavBar", parent, Ui.Panel);
            var nr = nav.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 1f);
            nr.anchorMax = new Vector2(1f, 1f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(0f, navHeight);
            nr.anchoredPosition = new Vector2(0f, -PhoneShell.StatusH);

            _backButton = Ui.TextButton("Back", nav.transform, "<", Ui.Panel, Ui.Accent, 18, Back).gameObject;
            var br = _backButton.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 0.5f);
            br.sizeDelta = new Vector2(44f, 0f);

            _homeButton = Ui.TextButton("Home", nav.transform, "\u2302", Ui.Panel, Ui.Accent, 18, ShowHome).gameObject;
            var hr = _homeButton.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(1f, 0f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(1f, 0.5f);
            hr.sizeDelta = new Vector2(44f, 0f);

            _titleLabel = Ui.Label("AppTitle", nav.transform, "", 15, TextAnchor.MiddleCenter, Ui.Text);
            var tr = _titleLabel.GetComponent<RectTransform>();
            Ui.Fill(tr);
            tr.offsetMin = new Vector2(44f, 0f);
            tr.offsetMax = new Vector2(-44f, 0f);
        }

        public Transform BuildAppArea(Transform parent, float navHeight)
        {
            var area = Ui.Rect("AppArea", parent, Ui.Bg);
            var ar = area.GetComponent<RectTransform>();
            Ui.Fill(ar);
            ar.offsetMax = new Vector2(0f, -(PhoneShell.StatusH + navHeight));
            return area.transform;
        }

        public void Register(PhoneApp app, Transform appArea, bool isHome = false)
        {
            app.Initialize(this, appArea);
            _apps.Add(app);
            if (isHome) _home = app;
        }

        public void ShowHome()
        {
            while (_stack.Count > 0) PopNoRefresh();
            Push(_home);
        }

        public void Push(PhoneApp app)
        {
            if (app == null) return;

            var leaving = Current;
            if (leaving != null)
            {
                leaving.Root.SetActive(false);
                SafeClosed(leaving);
            }

            _stack.Push(app);
            app.Root.SetActive(true);
            SafeOpened(app);
            Refresh();   // must run even if the app threw, or nav controls stay stale
        }

        public void Back()
        {
            if (_stack.Count <= 1) return; // home stays
            PopNoRefresh();
            var now = Current;
            if (now != null)
            {
                now.Root.SetActive(true);
                SafeOpened(now);
            }
            Refresh();
        }

        private void PopNoRefresh()
        {
            var app = _stack.Pop();
            app.Root.SetActive(false);
            SafeClosed(app);
        }

        /// <summary>
        /// App callbacks are isolated because a throw inside one would otherwise skip
        /// Refresh() and leave navigation wedged — back and home buttons stuck in
        /// whatever state they were in, with no visible connection to the real cause.
        /// A broken app should be a broken screen, not a broken phone.
        /// </summary>
        private static void SafeOpened(PhoneApp app)
        {
            try { app.OnOpened(); }
            catch (System.Exception e) { PhoneMod.Log.Error($"{app.Title}.OnOpened threw: {e}"); }
        }

        private static void SafeClosed(PhoneApp app)
        {
            try { app.OnClosed(); }
            catch (System.Exception e) { PhoneMod.Log.Error($"{app.Title}.OnClosed threw: {e}"); }
        }

        private void Refresh()
        {
            if (_titleLabel != null) _titleLabel.text = Current?.Title ?? "";
            var deep = _stack.Count > 1;
            if (_backButton != null) _backButton.SetActive(deep);
            if (_homeButton != null) _homeButton.SetActive(deep);
        }
    }
}
