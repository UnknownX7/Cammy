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
        private static IntPtr foVDeltaPtr;
        public static ref float FoVDelta => ref *(float*)foVDeltaPtr; // 0.08726646751

        public static float cameraHeightOffset = 0;
        public static float cameraSideOffset = 0;
        private delegate void GetCameraPositionDelegate(GameCamera* camera, IntPtr target, float* vectorPosition, bool swapPerson);
        private static Hook<GetCameraPositionDelegate> GetCameraPositionHook;
        private static void GetCameraPositionDetour(GameCamera* camera, IntPtr target, float* vectorPosition, bool swapPerson)
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

        public static bool IsSpectating { get; private set; } = false;
        public delegate IntPtr GetCameraTargetDelegate(IntPtr camera);
        public static Hook<GetCameraTargetDelegate> GetCameraTargetHook;
        private static IntPtr GetCameraTargetDetour(IntPtr camera)
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


        private delegate byte GetCameraAutoRotateModeDelegate(IntPtr camera, IntPtr framework);
        private static Hook<GetCameraAutoRotateModeDelegate> GetCameraAutoRotateModeHook;
        private static byte GetCameraAutoRotateModeDetour(IntPtr camera, IntPtr framework) => (byte)(FreeCam.Enabled || IsSpectating ? 4 : GetCameraAutoRotateModeHook.Original(camera, framework));

        private static IntPtr inputData;
        private static delegate* unmanaged<Framework*, IntPtr> getInputData;

        private static delegate* unmanaged<IntPtr, int, byte> isInputIDHeld;
        public static bool IsInputIDHeld(int i) => isInputIDHeld(inputData, i) != 0;

        private static delegate* unmanaged<IntPtr, int, byte> isInputIDPressed;
        public static bool IsInputIDPressed(int i) => isInputIDPressed(inputData, i) != 0;

        private static delegate* unmanaged<IntPtr, int, byte> isInputIDLongPressed;
        public static bool IsInputIDLongPressed(int i) => isInputIDLongPressed(inputData, i) != 0;

        private static delegate* unmanaged<IntPtr, int, byte> isInputIDReleased;
        public static bool IsInputIDReleased(int i) => isInputIDReleased(inputData, i) != 0;

        private static delegate* unmanaged<IntPtr, int, int> getAnalogInputID;
        public static float GetAnalogInputID(int i) => getAnalogInputID(inputData, i) / 100f;

        private static delegate* unmanaged<sbyte> getMouseWheelStatus;
        public static sbyte GetMouseWheelStatus() => getMouseWheelStatus();

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

        public static void Initialize()
        {
            cameraManager = (CameraManager*)DalamudApi.SigScanner.GetStaticAddressFromSig("4C 8D 35 ?? ?? ?? ?? 85 D2"); // g_ControlSystem_CameraManager

            var vtbl = cameraManager->WorldCamera->VTable;
            GetCameraPositionHook = new(vtbl[15], GetCameraPositionDetour); // Client__Game__Camera_vf15
            GetCameraTargetHook = new(vtbl[17], GetCameraTargetDetour); // Client__Game__Camera_vf17
            CanChangePerspectiveHook = new(vtbl[22], CanChangePerspectiveDetour); // Client__Game__Camera_vf22
            GetZoomDeltaHook = new(vtbl[28], GetZoomDeltaDetour); // Client__Game__Camera_vf28
            GetCameraAutoRotateModeHook = new(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB 85 C0 0F 84 ?? ?? ?? ?? 83 E8 01"), GetCameraAutoRotateModeDetour); // Found inside Client__Game__Camera_UpdateRotation
            GetCameraPositionHook.Enable();
            GetCameraTargetHook.Enable();
            CanChangePerspectiveHook.Enable();
            GetZoomDeltaHook.Enable();
            GetCameraAutoRotateModeHook.Enable();

            foVDeltaPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F"); // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
            forceDisableMovementPtr = DalamudApi.SigScanner.GetStaticAddressFromSig("48 83 EC 28 83 3D ?? ?? ?? ?? ?? 0F 87") + 1; // Why is this 1 off? (Also found at g_PlayerMoveController + 0x51C)

            getInputData = (delegate* unmanaged<Framework*, IntPtr>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 80 BB A2 00 00 00 00");
            isInputIDHeld = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? BA 4D 01 00 00");
            isInputIDPressed = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E9 ?? ?? ?? ?? 83 7F 44 02");
            isInputIDLongPressed = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 08 85 DB");
            isInputIDReleased = (delegate* unmanaged<IntPtr, int, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 88 43 0F");
            getAnalogInputID = (delegate* unmanaged<IntPtr, int, int>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 66 44 0F 6E C3");
            getMouseWheelStatus = (delegate* unmanaged<sbyte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? F7 D8 48 8B CB");
            inputData = getInputData(Framework.Instance());
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
            GetCameraTargetHook?.Dispose();
            CanChangePerspectiveHook?.Dispose();
            GetZoomDeltaHook?.Dispose();
            GetCameraAutoRotateModeHook?.Dispose();
        }
    }
}
