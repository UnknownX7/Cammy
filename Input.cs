using System;
using System.Data;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Cammy {
    // Most of this is stolen from QoLBar
    public unsafe class Input
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        public static bool IsGameFocused
        {
            get
            {
                var activatedHandle = GetForegroundWindow();
                if (activatedHandle == IntPtr.Zero)
                    return false;

                var procId = Environment.ProcessId;
                _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);

                return activeProcId == procId;
            }
        }
    
        private static IntPtr isTextInputActivePtr = IntPtr.Zero;
        private static bool IsGameTextInputActive => isTextInputActivePtr != IntPtr.Zero && *(bool*)isTextInputActivePtr;
    
        public static bool Disabled => IsGameTextInputActive || !IsGameFocused || ImGui.GetIO().WantCaptureKeyboard;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);
        private static readonly byte[] keyboardState = new byte[256];

        public Input()
        {
            try { isTextInputActivePtr = *(IntPtr*)((IntPtr)AtkStage.GetSingleton() + 0x28) + 0x188E; } // Located in AtkInputManager
            catch { PluginLog.LogError("Failed loading textActiveBoolPtr"); }
        }

        public void Update()
        {
            GetKeyboardState(keyboardState);
        }

        public bool IsDown(VirtualKey key) => (keyboardState[(int)key] & 0x80) != 0;
    }
}