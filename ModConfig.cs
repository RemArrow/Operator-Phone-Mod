using MelonLoader;
using UnityEngine;

namespace OperatorPhone
{
    internal static class ModConfig
    {
        private static MelonPreferences_Category _cat;

        private static MelonPreferences_Entry<KeyCode> _toggleKey;
        private static MelonPreferences_Entry<KeyCode> _probeKey;
        private static MelonPreferences_Entry<KeyCode> _dumpKey;
        private static MelonPreferences_Entry<bool> _probeEnabled;
        private static MelonPreferences_Entry<bool> _blockGameInput;
        private static MelonPreferences_Entry<float> _uiScale;
        private static MelonPreferences_Entry<string> _debugNumber;

        public static KeyCode ToggleKey => _toggleKey.Value;
        public static KeyCode ProbeKey => _probeKey.Value;
        public static KeyCode DumpKey => _dumpKey.Value;
        public static bool ProbeEnabled => _probeEnabled.Value;
        public static bool BlockGameInput => _blockGameInput.Value;
        public static float UiScale => _uiScale.Value;
        public static string DebugNumber => _debugNumber.Value;
        // Service endpoints are compiled in (ServiceConfig) so the mod ships as one
        // DLL with no setup. These forward for existing call sites.
        public static string WorkerUrl => ServiceConfig.WorkerUrl;
        public static string PhotonChatAppId => ServiceConfig.PhotonChatAppId;

        public static void Load()
        {
            _cat = MelonPreferences.CreateCategory("OperatorPhone", "Operator Phone");

            _toggleKey = _cat.CreateEntry("ToggleKey", KeyCode.F1,
                description: "Opens and closes the phone.");

            _probeEnabled = _cat.CreateEntry("ProbeEnabled", true,
                description: "M0 only. Enables the input-system probe. Turn off for release.");

            _probeKey = _cat.CreateEntry("ProbeKey", KeyCode.F10,
                description: "Dumps input-system diagnostics to the MelonLoader log.");

            _dumpKey = _cat.CreateEntry("DumpKey", KeyCode.F11,
                description: "Dumps InputLayer/DreamOS reconnaissance to UserData.");

            _blockGameInput = _cat.CreateEntry("BlockGameInput", true,
                description: "Suppress game input while the phone is open. " +
                             "Disable if it conflicts with OPERATOR's input handling.");

            _uiScale = _cat.CreateEntry("UiScale", 1.0f,
                description: "Phone panel scale multiplier.");

            _debugNumber = _cat.CreateEntry("DebugNumber", "201-4417",
                description: "Unused since M1 — numbers now come from the identity worker.");




            _cat.SaveToFile(false);
        }
    }
}