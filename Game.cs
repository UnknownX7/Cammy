using System;
using System.Numerics;
using Cammy.Structures;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace Cammy
{
    public static unsafe class Game
    {
        public static CameraManager* cameraManager;
        public static GameCamera* freeCam;
        public static float? cachedDefaultLookAtHeight = null;
        public static bool IsFreeCamEnabled => freeCam != null;
        private static Vector3 freeCamPositionOffset;

        // This variable is merged with a lot of other constants so it's not possible to change normally
        public static float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private static Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private static float GetZoomDeltaDetour() => zoomDelta;

        // Of course this isn't though
        private static IntPtr foVDeltaPtr;
        public static ref float FoVDelta => ref *(float*)foVDeltaPtr; // 0.08726646751

        public static float cameraHeightOffset = 0;
        private delegate void GetCameraPositionDelegate(IntPtr camera, IntPtr target, float* vectorPosition, bool swapPerson);
        private static Hook<GetCameraPositionDelegate> GetCameraPositionHook;
        private static void GetCameraPositionDetour(IntPtr camera, IntPtr target, float* vectorPosition, bool swapPerson)
        {
            if (!IsFreeCamEnabled)
            {
                GetCameraPositionHook.Original(camera, target, vectorPosition, swapPerson);
                vectorPosition[1] += cameraHeightOffset;
            }
            else
            {
                vectorPosition[0] = freeCamPositionOffset.X;
                vectorPosition[1] = freeCamPositionOffset.Z;
                vectorPosition[2] = freeCamPositionOffset.Y;
            }
        }

        public static bool IsSpectating { get; private set; } = false;
        public delegate IntPtr GetCameraTargetDelegate(IntPtr camera);
        public static Hook<GetCameraTargetDelegate> GetCameraTargetHook;
        private static IntPtr GetCameraTargetDetour(IntPtr camera)
        {
            if (DalamudApi.TargetManager.FocusTarget is { } focus)
            {
                IsSpectating = true;
                return focus.Address;
            }

            if (DalamudApi.TargetManager.SoftTarget is { } soft)
            {
                IsSpectating = true;
                return soft.Address;
            }

            IsSpectating = false;
            return DalamudApi.ClientState.LocalPlayer is { } player ? player.Address : IntPtr.Zero;
        }

        private delegate byte GetCameraAutoRotateModeDelegate(IntPtr camera, IntPtr framework);
        private static Hook<GetCameraAutoRotateModeDelegate> GetCameraAutoRotateModeHook;
        private static byte GetCameraAutoRotateModeDetour(IntPtr camera, IntPtr framework) => (byte)(IsFreeCamEnabled || IsSpectating ? 4 : GetCameraAutoRotateModeHook.Original(camera, framework));

        private static IntPtr inputData;
        private static delegate* unmanaged<Framework*, IntPtr> getInputData;

        private static delegate* unmanaged<IntPtr, int, byte> isInputIDHeld;
        public static bool IsInputIDHeld(int i) => isInputIDHeld(inputData, i) != 0;

        //private static delegate* unmanaged<IntPtr, int, byte> isInputIDPressed;
        //public static bool IsInputIDPressed(int i) => isInputIDPressed(inputData, i) != 0;

        public static IntPtr forceDisableMovementPtr;
        public static ref int ForceDisableMovement => ref *(int*)forceDisableMovementPtr; // Increments / decrements by 1 to allow multiple things to disable movement at the same time

        public static readonly Memory.Replacer cameraNoCollideReplacer = new("E8 ?? ?? ?? ?? 45 0F 57 FF", new byte[] { 0x30, 0xC0, 0x90, 0x90, 0x90 }); // E8 ?? ?? ?? ?? 48 8B B4 24 E0 00 00 00 40 32 FF (0x90, 0x90, 0x90, 0x90, 0x90)

        public static bool isLoggedIn = false;
        public static bool onLogin = false;
        public static int changingAreaDelay = 0;
        public static bool isChangingAreas = false;

        // Very optimized very good
        public static float GetDefaultLookAtHeightOffset()
        {
            if (cachedDefaultLookAtHeight.HasValue)
                return cachedDefaultLookAtHeight.Value;

            if (cameraManager == null) return 0;

            var worldCamera = cameraManager->WorldCamera;

            if (worldCamera == null) return 0;

            var prev = worldCamera->LookAtHeightOffset;
            worldCamera->ResetLookatHeightOffset = 1;
            ((delegate* unmanaged<GameCamera*, void>)worldCamera->VTable[2])(worldCamera);
            var ret = worldCamera->LookAtHeightOffset;
            worldCamera->LookAtHeightOffset = prev;
            cachedDefaultLookAtHeight = ret;
            return ret;
        }

        public static void ToggleFreeCam()
        {
            var enable = !IsFreeCamEnabled;
            var isMainMenu = !DalamudApi.Condition.Any();
            if (enable)
            {
                freeCamPositionOffset = DalamudApi.ClientState.LocalPlayer?.Position is { } pos ? new(pos.X, pos.Z, pos.Y + 1) : new();
                freeCam = isMainMenu ? cameraManager->MenuCamera : cameraManager->WorldCamera;
                if (isMainMenu)
                    *(byte*)((IntPtr)freeCam + 0x2A0) = 0;
                freeCam->MinVRotation = -1.559f;
                freeCam->MaxVRotation = 1.559f;
                freeCam->CurrentFoV = freeCam->MinFoV = freeCam->MaxFoV = 0.78f;
                freeCam->MinZoom = 0;
                freeCam->CurrentZoom = freeCam->MaxZoom = 0.1f;
                zoomDelta = 0;
                freeCam->AddedFoV = freeCam->LookAtHeightOffset = 0;
                freeCam->Mode = 1;
                cameraNoCollideReplacer.Enable();

                if (!isMainMenu)
                {
                    ForceDisableMovement++;
                    Cammy.PrintEcho("Controls: Move Keybinds - Move, Jump / Ascend - Up, Descend - Down, Shift (Hold) - Speed up, C - Reset, Esc - Stop Free Cam");
                }
            }
            else
            {
                freeCam = null;
                cameraNoCollideReplacer.Disable();

                if (!isMainMenu)
                {
                    if (ForceDisableMovement > 0)
                        ForceDisableMovement--;
                    new CameraConfigPreset().Apply();
                    PresetManager.DisableCameraPresets();
                }
            }

            if (!isMainMenu) return;

            static void ToggleAddonVisible(string name)
            {
                var addon = DalamudApi.GameGui.GetAddonByName(name, 1);
                if (addon == IntPtr.Zero) return;
                ((AtkUnitBase*)addon)->IsVisible ^= true;
            }

            ToggleAddonVisible("_TitleRights");
            ToggleAddonVisible("_TitleRevision");
            ToggleAddonVisible("_TitleMenu");
            ToggleAddonVisible("_TitleLogo");
        }

        public static void Initialize()
        {
            cameraManager = (CameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 34 C6 F3"); // g_ControlSystem_CameraManager

            var vtbl = cameraManager->WorldCamera->VTable;
            GetCameraPositionHook = new(vtbl[14], GetCameraPositionDetour); // Client__Game__Camera_vf14
            GetCameraTargetHook = new(vtbl[16], GetCameraTargetDetour); // Client__Game__Camera_vf16
            GetZoomDeltaHook = new(vtbl[27], GetZoomDeltaDetour); // Client__Game__Camera_vf27
            GetCameraAutoRotateModeHook = new(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB 85 C0 0F 84 ?? ?? ?? ?? 83 E8 01"), GetCameraAutoRotateModeDetour); // Found inside Client__Game__Camera_UpdateRotation
            GetCameraPositionHook.Enable();
            GetZoomDeltaHook.Enable();
            GetCameraAutoRotateModeHook.Enable();

            foVDeltaPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
            forceDisableMovementPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("48 83 EC 28 83 3D ?? ?? ?? ?? ?? 0F 87") + 1; // Why is this 1 off? (Also found at g_PlayerMoveController + 0x51C)

            getInputData = (delegate* unmanaged<Framework*, IntPtr>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 80 BB A2 00 00 00 00");
            isInputIDHeld = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? BA 4D 01 00 00");
            //isInputIDPressed = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? 83 7F 44 02");
            inputData = getInputData(Framework.Instance());
        }

        public static void Update()
        {
            //for (int i = 0; i < 2000; i++)
            //{
            //    if (isInputIDPressed(inputData, i) > 0)
            //        PluginLog.Error($"{i}");
            //}

            if (onLogin)
                onLogin = false;

            if (!isLoggedIn)
                onLogin = isLoggedIn = DalamudApi.ClientState.IsLoggedIn && !DalamudApi.Condition[ConditionFlag.BetweenAreas];

            if (isChangingAreas && !DalamudApi.Condition[ConditionFlag.BetweenAreas] && --changingAreaDelay == 0)
                isChangingAreas = false;

            if (IsFreeCamEnabled)
                UpdateFreeCam();
        }

        public static void UpdateFreeCam()
        {
            var keyState = DalamudApi.KeyState;

            var loggedIn = DalamudApi.ClientState.IsLoggedIn;

            // IsInputIDHeld(3) // Cant block Esc from the game when doing this
            if (keyState[27] || (loggedIn ? ForceDisableMovement == 0 : DalamudApi.GameGui.GetAddonByName("Title", 1) == IntPtr.Zero)) // Esc
            {
                DalamudApi.KeyState[27] = false;
                ToggleFreeCam();
                return;
            }

            var movePos = Vector3.Zero;

            //if (keyState[87]) // W
            if (IsInputIDHeld(321) || IsInputIDHeld(36) && IsInputIDHeld(37)) // Move Forward / Left + Right Click
                movePos.X += 1;

            //if (keyState[65]) // A
            if (IsInputIDHeld(323) || IsInputIDHeld(325)) // Move / Strafe Left
                movePos.Y += 1;

            //if (keyState[83]) // S
            if (IsInputIDHeld(322)) // Move Back
                movePos.X += -1;

            //if (keyState[68]) // D
            if (IsInputIDHeld(324) || IsInputIDHeld(326)) // Move / Strafe Right
                movePos.Y += -1;

            //if (keyState[32]) // Space
            if (IsInputIDHeld(348) || IsInputIDHeld(444)) // Jump / Ascend
                movePos.Z += 1;

            //if (keyState[32] && ImGui.GetIO().KeyCtrl) // Ctrl + Space
            if (IsInputIDHeld(443)) // Descent
                movePos.Z -= 1;

            if (keyState[67]) // C
            {
                DalamudApi.KeyState[67] = false;
                if (loggedIn)
                {
                    freeCamPositionOffset = DalamudApi.ClientState.LocalPlayer?.Position is { } pos ? new(pos.X, pos.Z, pos.Y + 1) : new();
                }
                else
                {
                    freeCam->X = 0;
                    freeCam->Y = 0;
                    freeCam->Z = 0;
                    freeCam->Z2 = 0;
                }
            }

            if (movePos == Vector3.Zero) return;

            movePos *= (float)(DalamudApi.Framework.UpdateDelta.TotalSeconds * 20);

            if (ImGui.GetIO().KeyShift)
                movePos *= 10;
            const double halfPI = Math.PI / 2f;
            var hAngle = freeCam->CurrentHRotation + halfPI;
            var vAngle = freeCam->CurrentVRotation;
            var direction = new Vector3((float)(Math.Cos(hAngle) * Math.Cos(vAngle)), -(float)(Math.Sin(hAngle) * Math.Cos(vAngle)), (float)Math.Sin(vAngle));

            var amount = direction * movePos.X;
            var x = amount.X + movePos.Y * (float)Math.Sin(freeCam->CurrentHRotation - halfPI);
            var y = amount.Y + movePos.Y * (float)Math.Cos(freeCam->CurrentHRotation - halfPI);
            var z = amount.Z + movePos.Z;

            if (loggedIn)
            {
                freeCamPositionOffset.X += x;
                freeCamPositionOffset.Y += y;
                freeCamPositionOffset.Z += z;
            }
            else
            {
                freeCam->X += x;
                freeCam->Y += y;
                freeCam->Z2 = freeCam->Z += z;
            }
        }

        public static void Dispose()
        {
            if (IsFreeCamEnabled)
                ToggleFreeCam();

            GetCameraPositionHook?.Dispose();
            GetCameraAutoRotateModeHook?.Dispose();
            GetCameraTargetHook?.Dispose();
            GetZoomDeltaHook?.Dispose();
        }
    }
}
