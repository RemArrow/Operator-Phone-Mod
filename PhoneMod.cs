using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using OperatorPhone.Diagnostics;
using OperatorPhone.Input;
using OperatorPhone.UI;
using System;
using UnityEngine;
using static Il2CppSystem.Globalization.CultureInfo;

namespace OperatorPhone
{
    /// <summary>
    /// M0 harness. Goal is narrow: prove the canvas renders, prove open/close works,
    /// prove input capture is clean. M1 adds identity; Photon arrives in M2.
    /// </summary>
    public class PhoneMod : MelonMod
    {
        public static PhoneMod Instance { get; private set; }
        public static MelonLogger.Instance Log => Instance.LoggerInstance;

        private GameObject _root;
        private PhoneShell _shell;
        private bool _bootstrapped;
        private static bool _chatFaulted;

        public override void OnInitializeMelon()
        {
            Instance = this;
            ModConfig.Load();
            Data.ContactStore.Load();

            // MonoBehaviours must exist in the Il2Cpp domain before we can AddComponent them.
            // Order matters: register before anything tries to instantiate.
            TryRegister<PhoneShell>();
            TryRegister<InputGate>();

            HarmonyInstance.PatchAll(typeof(PhoneMod).Assembly);

            LoggerInstance.Msg("Initialised. Waiting for a scene before bootstrapping UI.");
        }

        private void TryRegister<T>() where T : MonoBehaviour
        {
            try
            {
                if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
                    ClassInjector.RegisterTypeInIl2Cpp<T>();
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error($"Failed to register {typeof(T).Name} in Il2Cpp: {e}");
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // Deliberately deferred. Creating a Canvas or RenderTexture before Unity's
            // graphics device is up will hard-crash IL2CPP — same class of bug that bit
            // OperatorVRHead. Wait for a real scene, then build once.
            if (_bootstrapped) return;

            try
            {
                Bootstrap();
                _bootstrapped = true;
                LoggerInstance.Msg($"UI bootstrapped in scene '{sceneName}'.");

                // Deferred a frame past bootstrap: Steamworks needs the game's own init
                // to have completed, and the splash scene is early enough that it may
                // not have.
                MelonCoroutines.Start(StartIdentityAfterDelay());
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error($"Bootstrap failed: {e}");
            }
        }

        private void Bootstrap()
        {
            _root = new GameObject("OperatorPhone_Root");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.hideFlags = HideFlags.HideAndDontSave;

            _root.AddComponent<InputGate>();
            _shell = _root.AddComponent<PhoneShell>();
        }

        private System.Collections.IEnumerator StartIdentityAfterDelay()
        {
            yield return new UnityEngine.WaitForSeconds(3f);

            Identity.IdentityService.OnChanged += OnIdentityChanged;
            Identity.IdentityService.Begin();
        }

        public override void OnUpdate()
        {
            if (!_bootstrapped) return;

            // Input FIRST, always. Anything below can throw — a missing dependency, a
            // network hiccup, a bad frame from Photon — and if the toggle sat downstream
            // of that, one exception would silently make the phone unopenable with no
            // obvious connection between cause and symptom.
            if (UnityEngine.Input.GetKeyDown(ModConfig.ToggleKey))
                _shell?.Toggle();

            if (ModConfig.ProbeEnabled)
            {
                if (UnityEngine.Input.GetKeyDown(ModConfig.ProbeKey))
                    InputProbe.Run();
                if (UnityEngine.Input.GetKeyDown(ModConfig.DumpKey))
                    Diagnostics.TypeDumper.Run();
            }

            PumpChat();
        }

        /// <summary>
        /// Photon's .NET client is not threaded: it only sends, receives, and dispatches
        /// callbacks inside Service(). Miss this and nothing moves.
        ///
        /// Latched because merely touching ChatService triggers loading PhotonClient,
        /// and a failure there recurs every frame. One report, then stop.
        /// </summary>
        private void PumpChat()
        {
            if (_chatFaulted) return;

            try { Chat.ChatService.Instance?.Pump(); }
            catch (System.Exception e)
            {
                _chatFaulted = true;
                LoggerInstance.Error("Chat disabled for this session.\n" + e);
            }
        }

        private static void OnIdentityChanged()
        {
            if (_chatFaulted) return;

            try
            {
                // Chat can only connect once there's a token to authenticate with.
                if (Identity.IdentityService.State == Identity.IdentityState.Ready)
                    Chat.ChatService.Start();
                else
                    Chat.ChatService.Stop();
            }
            catch (System.Exception e)
            {
                _chatFaulted = true;
                Log.Error("Chat unavailable.\n" + e);
            }
        }

        public override void OnApplicationQuit()
        {
            // Release the cursor if we're holding it, or the player gets a stuck cursor
            // in whatever they alt-tab to next.
            InputGate.Instance?.ForceRelease();
            if (!_chatFaulted)
            {
                try { Chat.ChatService.Stop(); } catch { }
            }
        }
    }
}