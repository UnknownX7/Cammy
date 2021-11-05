using System;
using System.Numerics;
using Cammy.Structures;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
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

        public delegate IntPtr GetCameraTargetDelegate(IntPtr camera);
        public static Hook<GetCameraTargetDelegate> GetCameraTargetHook;
        private static IntPtr GetCameraTargetDetour(IntPtr camera)
        {
            if (DalamudApi.TargetManager.FocusTarget is { } focus)
                return focus.Address;

            if (DalamudApi.TargetManager.SoftTarget is { } soft)
                return soft.Address;

            return DalamudApi.ClientState.LocalPlayer is { } player ? player.Address : IntPtr.Zero;
        }

        public static float cameraHeightOffset = 0;
        private delegate void GetCameraPositionDelegate(IntPtr camera, IntPtr target, float* vectorPosition, bool swapPerson);
        private static Hook<GetCameraPositionDelegate> GetCameraPositionHook;
        private static void GetCameraPositionDetour(IntPtr camera, IntPtr target, float* vectorPosition, bool swapPerson)
        {
            GetCameraPositionHook.Original(camera, target, vectorPosition, swapPerson);
            if (!IsFreeCamEnabled)
            {
                vectorPosition[1] += cameraHeightOffset;
            }
            else
            {
                vectorPosition[0] += freeCamPositionOffset.X;
                vectorPosition[1] += freeCamPositionOffset.Z;
                vectorPosition[2] += freeCamPositionOffset.Y;
            }
        }

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
                freeCamPositionOffset = new();
                freeCam = isMainMenu ? cameraManager->MenuCamera : cameraManager->WorldCamera;
                if (isMainMenu)
                    *(byte*)((IntPtr)freeCam + 0x2A0) = 0;
                freeCam->MinVRotation = -1.559f;
                freeCam->MaxVRotation = 1.559f;
                freeCam->CurrentFoV = freeCam->MinFoV = freeCam->MaxFoV = 0.78f;
                freeCam->CurrentZoom = freeCam->MinZoom = freeCam->MaxZoom = 0.1f;
                freeCam->AddedFoV = 0;
                cameraNoCollideReplacer.Enable();

                if (!isMainMenu)
                {
                    ForceDisableMovement++;
                    Cammy.PrintEcho("Controls: WASD - Move, Space - Up, Shift (Hold) - Speed up, C - Reset, Esc - Stop Free Cam");
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
            GetCameraPositionHook.Enable();
            GetZoomDeltaHook.Enable();

            foVDeltaPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
            forceDisableMovementPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("48 83 EC 28 83 3D ?? ?? ?? ?? ?? 0F 87") + 1; // Why is this 1 off? (Also found at g_PlayerMoveController + 0x51C)
        }

        public static void Update()
        {
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
            if (keyState[27] || !loggedIn && DalamudApi.GameGui.GetAddonByName("Title", 1) == IntPtr.Zero || ForceDisableMovement == 0) // Esc
            {
                DalamudApi.KeyState[27] = false;
                ToggleFreeCam();
                return;
            }

            var movePos = Vector3.Zero;

            if (keyState[87]) // W
                movePos.X += 1;

            if (keyState[65]) // A
                movePos.Y += 1;

            if (keyState[83]) // S
                movePos.X += -1;

            if (keyState[68]) // D
                movePos.Y += -1;

            if (keyState[32]) // Space
                movePos.Z += 1;

            if (keyState[67]) // C
            {
                DalamudApi.KeyState[67] = false;
                if (loggedIn)
                {
                    freeCamPositionOffset = new();
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
            var hAngle = freeCam->HRotation + halfPI;
            var vAngle = freeCam->CurrentVRotation;
            var direction = new Vector3((float)(Math.Cos(hAngle) * Math.Cos(vAngle)), -(float)(Math.Sin(hAngle) * Math.Cos(vAngle)), (float)Math.Sin(vAngle));

            var amount = direction * movePos.X;
            var x = amount.X + movePos.Y * (float)Math.Sin(freeCam->HRotation - halfPI);
            var y = amount.Y + movePos.Y * (float)Math.Cos(freeCam->HRotation - halfPI);
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
            GetCameraTargetHook?.Dispose();
            GetZoomDeltaHook?.Dispose();
        }
    }
}
