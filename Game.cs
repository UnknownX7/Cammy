using System;
using Cammy.Structures;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Cammy
{
    public static unsafe class Game
    {
        public static CameraManager* cameraManager;

        public static bool EnableSpectating { get; set; } = false;
        public static float? cachedDefaultLookAtHeight = null;

        // This variable is merged with a lot of other constants so it's not possible to change normally
        public static float zoomDelta = 0.75f;
        private delegate float GetZoomDeltaDelegate();
        private static Hook<GetZoomDeltaDelegate> GetZoomDeltaHook;
        private static float GetZoomDeltaDetour() => zoomDelta;

        // Of course this isn't though
        private static nint foVDeltaPtr;
        public static ref float FoVDelta => ref *(float*)foVDeltaPtr; // 0.08726646751

        public static float cameraHeightOffset = 0;
        public static float cameraSideOffset = 0;
        private delegate void GetCameraPositionDelegate(GameCamera* camera, nint target, float* vectorPosition, bool swapPerson);
        private static Hook<GetCameraPositionDelegate> GetCameraPositionHook;
        private static void GetCameraPositionDetour(GameCamera* camera, nint target, float* vectorPosition, bool swapPerson)
        {
            if (!FreeCam.Enabled)
            {
                GetCameraPositionHook.Original(camera, target, vectorPosition, swapPerson);
                vectorPosition[1] += cameraHeightOffset;

                if (cameraSideOffset == 0 || camera->Mode != 1) return;

                const float halfPI = MathF.PI / 2f;
                var a = cameraManager->WorldCamera->CurrentHRotation - halfPI;
                vectorPosition[0] += -cameraSideOffset * MathF.Sin(a);
                vectorPosition[2] += -cameraSideOffset * MathF.Cos(a);
            }
            else
            {
                vectorPosition[0] = FreeCam.position.X;
                vectorPosition[1] = FreeCam.position.Z;
                vectorPosition[2] = FreeCam.position.Y;
            }
        }

        public delegate void SetCameraLookAtDelegate(nint camera, float* lookAtPosition, float* cameraPosition, float* a4);
        public static Hook<SetCameraLookAtDelegate> SetCameraLookAtHook;
        private static void SetCameraLookAtDetour(nint camera, float* lookAtPosition, float* cameraPosition, float* a4) // a4 seems to be immediately overwritten and unused
        {
            if (FreeCam.Enabled) return;
            SetCameraLookAtHook.Original(camera, lookAtPosition, cameraPosition, a4);
        }

        public static bool IsSpectating { get; private set; } = false;
        public delegate nint GetCameraTargetDelegate(nint camera);
        public static Hook<GetCameraTargetDelegate> GetCameraTargetHook;
        private static nint GetCameraTargetDetour(nint camera)
        {
            if (EnableSpectating)
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
            }

            if (Cammy.Config.DeathCamMode == 1 && DalamudApi.Condition[ConditionFlag.Unconscious] && DalamudApi.TargetManager.Target is { } target)
            {
                IsSpectating = true;
                return target.Address;
            }

            IsSpectating = false;
            return GetCameraTargetHook.Original(camera);
        }

        private delegate byte CanChangePerspectiveDelegate();
        private static Hook<CanChangePerspectiveDelegate> CanChangePerspectiveHook;
        private static byte CanChangePerspectiveDetour() => (byte)(FreeCam.Enabled ? 0 : 1);


        private delegate byte GetCameraAutoRotateModeDelegate(nint camera, nint framework);
        private static Hook<GetCameraAutoRotateModeDelegate> GetCameraAutoRotateModeHook;
        private static byte GetCameraAutoRotateModeDetour(nint camera, nint framework) => (byte)(FreeCam.Enabled || IsSpectating ? 4 : GetCameraAutoRotateModeHook.Original(camera, framework));

        public delegate float GetCameraMaxMaintainDistanceDelegate(GameCamera* camera);
        public static Hook<GetCameraMaxMaintainDistanceDelegate> GetCameraMaxMaintainDistanceHook;
        // The camera isn't even used in the function...
        private static float GetCameraMaxMaintainDistanceDetour(GameCamera* camera) => GetCameraMaxMaintainDistanceHook.Original(camera) is var ret && ret < 10f ? ret : camera->MaxZoom;

        private static nint inputData;
        private static delegate* unmanaged<Framework*, nint> getInputData;

        private static delegate* unmanaged<nint, int, byte> isInputIDHeld;
        public static bool IsInputIDHeld(int i) => isInputIDHeld(inputData, i) != 0;

        private static delegate* unmanaged<nint, int, byte> isInputIDPressed;
        public static bool IsInputIDPressed(int i) => isInputIDPressed(inputData, i) != 0;

        private static delegate* unmanaged<nint, int, byte> isInputIDLongPressed;
        public static bool IsInputIDLongPressed(int i) => isInputIDLongPressed(inputData, i) != 0;

        private static delegate* unmanaged<nint, int, byte> isInputIDReleased;
        public static bool IsInputIDReleased(int i) => isInputIDReleased(inputData, i) != 0;

        private static delegate* unmanaged<nint, int, int> getAnalogInputID;
        public static float GetAnalogInputID(int i) => getAnalogInputID(inputData, i) / 100f;

        private static delegate* unmanaged<sbyte> getMouseWheelStatus;
        public static sbyte GetMouseWheelStatus() => getMouseWheelStatus();

        public static nint forceDisableMovementPtr;
        public static ref int ForceDisableMovement => ref *(int*)forceDisableMovementPtr; // Increments / decrements by 1 to allow multiple things to disable movement at the same time

        public static readonly Memory.Replacer cameraNoCollideReplacer = new("E8 ?? ?? ?? ?? 45 0F 57 FF", new byte[] { 0x30, 0xC0, 0x90, 0x90, 0x90 }); // E8 ?? ?? ?? ?? 48 8B B4 24 E0 00 00 00 40 32 FF (0x90, 0x90, 0x90, 0x90, 0x90)

        public static Memory.Replacer addMidHookReplacer;

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
            ((delegate* unmanaged<GameCamera*, void>)worldCamera->VTable[3])(worldCamera);
            var ret = worldCamera->LookAtHeightOffset;
            worldCamera->LookAtHeightOffset = prev;
            cachedDefaultLookAtHeight = ret;
            return ret;
        }

        public static void Initialize()
        {
            cameraManager = (CameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig("4C 8D 35 ?? ?? ?? ?? 85 D2"); // g_ControlSystem_CameraManager

            var vtbl = cameraManager->WorldCamera->VTable;
            GetCameraPositionHook = new(vtbl[15], GetCameraPositionDetour); // Client__Game__Camera_vf15
            SetCameraLookAtHook = new(vtbl[14], SetCameraLookAtDetour); // Client__Game__Camera_vf14
            GetCameraTargetHook = new(vtbl[17], GetCameraTargetDetour); // Client__Game__Camera_vf17
            CanChangePerspectiveHook = new(vtbl[22], CanChangePerspectiveDetour); // Client__Game__Camera_vf22
            GetZoomDeltaHook = new(vtbl[28], GetZoomDeltaDetour); // Client__Game__Camera_vf28
            GetCameraAutoRotateModeHook = new(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB 85 C0 0F 84 ?? ?? ?? ?? 83 E8 01"), GetCameraAutoRotateModeDetour); // Found inside Client__Game__Camera_UpdateRotation
            var maintainDistanceAddress = DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? F3 0F 5D 44 24 58");
            GetCameraMaxMaintainDistanceHook = new(maintainDistanceAddress, GetCameraMaxMaintainDistanceDetour); // Found 1 function deep inside Client__Game__Camera_vf3
            GetCameraPositionHook.Enable();
            SetCameraLookAtHook.Enable();
            GetCameraTargetHook.Enable();
            CanChangePerspectiveHook.Enable();
            GetZoomDeltaHook.Enable();
            GetCameraAutoRotateModeHook.Enable();
            GetCameraMaxMaintainDistanceHook.Enable();

            foVDeltaPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
            forceDisableMovementPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 10 05 ?? ?? ?? ?? 0F 2E C6 0F 8A") + 4; // Also found at g_PlayerMoveController + 0x54C

            getInputData = (delegate* unmanaged<Framework*, nint>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 80 BB A2 00 00 00 00");
            isInputIDHeld = (delegate* unmanaged<nint, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? BA 4D 01 00 00");
            isInputIDPressed = (delegate* unmanaged<nint, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? 83 7F 44 02");
            isInputIDLongPressed = (delegate* unmanaged<nint, int, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 08 85 DB");
            isInputIDReleased = (delegate* unmanaged<nint, int, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 88 43 0F");
            getAnalogInputID = (delegate* unmanaged<nint, int, int>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 66 44 0F 6E C3");
            getMouseWheelStatus = (delegate* unmanaged<sbyte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? F7 D8 48 8B CB");
            inputData = getInputData(Framework.Instance());

            // Gross workaround for fixing legacy control's maintain distance
            var address = DalamudApi.SigScanner.ScanModule("48 85 C9 74 24 48 83 C1 10");
            var offset = BitConverter.GetBytes((long)maintainDistanceAddress - (long)(address + 0x8));

            // mov rcx, rbx
            // call offset
            // jmp 27h
            addMidHookReplacer = new(address,
                new byte[] {
                    0x48, 0x8B, 0xCB,
                    0xE8, offset[0], offset[1], offset[2], offset[3],
                    0xEB, 0x27,
                    0x90, 0x90, 0x90, 0x90
                },
                true);
        }

        public static void Update()
        {
            //for (int i = 0; i < 1000; i++)
            //{
            //    if (isInputIDPressed(inputData, i) > 0)
            //        Dalamud.Logging.PluginLog.Error($"{i}");
            //}

            if (onLogin)
                onLogin = false;

            if (!isLoggedIn)
                onLogin = isLoggedIn = DalamudApi.ClientState.IsLoggedIn && !DalamudApi.Condition[ConditionFlag.BetweenAreas];

            if (isChangingAreas && !DalamudApi.Condition[ConditionFlag.BetweenAreas] && --changingAreaDelay == 0)
                isChangingAreas = false;
        }

        public static void Dispose()
        {
            GetCameraPositionHook?.Dispose();
            SetCameraLookAtHook?.Dispose();
            GetCameraTargetHook?.Dispose();
            CanChangePerspectiveHook?.Dispose();
            GetZoomDeltaHook?.Dispose();
            GetCameraAutoRotateModeHook?.Dispose();
            GetCameraMaxMaintainDistanceHook?.Dispose();
        }
    }
}
