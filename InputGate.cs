using System;
using UnityEngine;

namespace OperatorPhone.Input
{
    /// <summary>
    /// Owns the "phone has input focus" state and the cursor.
    ///
    /// This is the M0 risk. If OPERATOR polls input through its own manager rather than
    /// UnityEngine.Input, the Harmony prefixes in LegacyInputPatches will no-op and the
    /// player will shoot their squad while typing. Run the probe (F10) before trusting it.
    /// </summary>
    public class InputGate : MonoBehaviour
    {
        public static InputGate Instance { get; private set; }

        /// <summary>True while the phone should own keyboard and mouse.</summary>
        public static bool Captured { get; private set; }

        private CursorLockMode _prevLock;
        private bool _prevVisible;
        private bool _restored = true;

        public InputGate(IntPtr ptr) : base(ptr) { }

        private void Awake() => Instance = this;

        public void Capture()
        {
            if (Captured) return;

            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
            _restored = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Captured = true;

            PhoneMod.Log.Msg($"Input captured (restoring to {_prevLock}/{_prevVisible} on release).");
        }

        public void Release()
        {
            if (!Captured) return;

            Captured = false;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            _restored = true;
        }

        public void ForceRelease()
        {
            Captured = false;
            if (_restored) return;
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
            _restored = true;
        }

        private void LateUpdate()
        {
            // OPERATOR will fight us for the cursor — most FPS games re-lock it every frame
            // from their own state machine. Reassert after their update has run.
            if (!Captured) return;

            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }

        private void OnDestroy()
        {
            ForceRelease();
            if (Instance == this) Instance = null;
        }
    }
}
