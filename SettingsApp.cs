using OperatorPhone.Chat;
using OperatorPhone.Identity;
using UnityEngine;

namespace OperatorPhone.UI.Apps
{
    internal class SettingsApp : PhoneApp
    {
        public override string Title => "Settings";
        public override string Glyph => "S";

        private UnityEngine.UI.Text _info;

        protected override void Build(Transform root)
        {
            _info = Ui.Label("Info", root, "", 13, TextAnchor.UpperLeft, Ui.Text, wrap: true);
            var ir = _info.GetComponent<RectTransform>();
            Ui.Fill(ir);
            ir.offsetMin = new Vector2(16f, 120f);
            ir.offsetMax = new Vector2(-16f, -16f);

            var unlink = Ui.TextButton("Unlink", root, "UNLINK STEAM ACCOUNT",
                new Color(0.34f, 0.16f, 0.16f, 1f), Color.white, 12, () =>
                {
                    // Deliberately immediate rather than confirm-dialogued: relinking
                    // is cheap (one browser trip) and the account/number survive on the
                    // server, so the worst case of a stray click is mild.
                    try { ChatService.Stop(); }
                    catch (System.Exception e) { PhoneMod.Log.Warning($"ChatService.Stop failed: {e.Message}"); }
                    IdentityService.Unlink();
                });
            var ur = unlink.GetComponent<RectTransform>();
            ur.anchorMin = new Vector2(0.5f, 0f);
            ur.anchorMax = new Vector2(0.5f, 0f);
            ur.pivot = new Vector2(0.5f, 0f);
            ur.sizeDelta = new Vector2(240f, 40f);
            ur.anchoredPosition = new Vector2(0f, 24f);
        }

        public override void OnOpened()
        {
            _info.text =
                $"Number: {IdentityService.Number ?? "\u2014"}\n" +
                $"Identity: {IdentityService.State}\n" +
                $"Chat: {ChatStatus()}\n\n" +
                $"Toggle key: {ModConfig.ToggleKey}\n" +
                "(rebind in UserData/MelonPreferences.cfg)";
        }

        /// <summary>
        /// Isolated in its own method with a catch because merely touching ChatService
        /// forces the CLR to load PhotonClient. If that assembly can't be resolved the
        /// throw happens here — and inlined into OnOpened it would take the whole
        /// settings screen (and previously, navigation itself) down with it.
        /// </summary>
        private static string ChatStatus()
        {
            try
            {
                var chat = ChatService.Instance;
                if (chat == null) return "off";
                if (chat.IsConnected) return "connected";
                return chat.LastError ?? "connecting";
            }
            catch (System.Exception e)
            {
                PhoneMod.Log.Warning($"Chat status unavailable: {e.Message}");
                return "unavailable";
            }
        }
    }
}
