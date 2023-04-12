using System;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Hypostasis.Game.Structures;

namespace Cammy;

[HypostasisInjection]
public static unsafe class Game
{
    public static bool EnableSpectating { get; set; } = false;
    public static float? CachedDefaultLookAtHeight { get; set; }

    // This variable is merged with a lot of other constants so it's not possible to change normally
    public static float ZoomDelta { get; set; } = 0.75f;
    private static float GetZoomDeltaDetour() => ZoomDelta;

    // Of course this isn't though
    [HypostasisSignatureInjection("F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F", Static = true, Required = true)] // F3 0F 59 05 ?? ?? ?? ?? 0F 28 74 24 20 48 83 C4 30 5B C3 0F 57 C0 0F 28 74 24 20 48 83 C4 30 5B C3
    private static float* foVDeltaPtr;
    public static float FoVDelta // 0.08726646751
    {
        get => foVDeltaPtr != null ? *foVDeltaPtr : 0;
        set
        {
            if (foVDeltaPtr != null)
                *foVDeltaPtr = value;
        }
    }

    private static void SetCameraLookAtDetour(GameCamera* camera, Vector3* lookAtPosition, Vector3* cameraPosition, Vector3* a4) // a4 seems to be immediately overwritten and unused
    {
        if (FreeCam.Enabled) return;
        camera->VTable.setCameraLookAt.Original(camera, lookAtPosition, cameraPosition, a4);
    }

    public static float CameraHeightOffset { get; set; }
    public static float CameraSideOffset { get; set; }
    private static void GetCameraPositionDetour(GameCamera* camera, GameObject* target, Vector3* position, Bool swapPerson)
    {
        if (!FreeCam.Enabled)
        {
            camera->VTable.getCameraPosition.Original(camera, target, position, swapPerson);
            position->Y += CameraHeightOffset;

            if (CameraSideOffset == 0 || camera->mode != 1) return;

            const float halfPI = MathF.PI / 2f;
            var a = Common.CameraManager->worldCamera->currentHRotation - halfPI;
            position->X += -CameraSideOffset * MathF.Sin(a);
            position->Z += -CameraSideOffset * MathF.Cos(a);
        }
        else
        {
            *position = FreeCam.Position;
        }
    }

    public static bool IsSpectating { get; private set; } = false;
    private static GameObject* GetCameraTargetDetour(GameCamera* camera)
    {
        if (EnableSpectating)
        {
            if (DalamudApi.TargetManager.FocusTarget is { } focus)
            {
                IsSpectating = true;
                return (GameObject*)focus.Address;
            }

            if (DalamudApi.TargetManager.SoftTarget is { } soft)
            {
                IsSpectating = true;
                return (GameObject*)soft.Address;
            }
        }

        if (Cammy.Config.DeathCamMode == 1 && DalamudApi.Condition[ConditionFlag.Unconscious] && DalamudApi.TargetManager.Target is { } target)
        {
            IsSpectating = true;
            return (GameObject*)target.Address;
        }

        IsSpectating = false;
        return camera->VTable.getCameraTarget.Original(camera);
    }

    private static Bool CanChangePerspectiveDetour() => !FreeCam.Enabled;

    private static byte GetCameraAutoRotateModeDetour(GameCamera* camera, Framework* framework) => (byte)(FreeCam.Enabled || IsSpectating ? 4 : GameCamera.getCameraAutoRotateMode.Original(camera, framework));

    private static float GetCameraMaxMaintainDistanceDetour(GameCamera* camera) => GameCamera.getCameraMaxMaintainDistance.Original(camera) is var ret && ret < 10f ? ret : camera->maxZoom;

    [HypostasisSignatureInjection("F3 0F 10 05 ?? ?? ?? ?? 0F 2E C6 0F 8A", Offset = 4, Static = true, Required = true)] // Also found at g_PlayerMoveController + 0x54C
    private static nint forceDisableMovementPtr;
    public static ref int ForceDisableMovement => ref *(int*)forceDisableMovementPtr; // Increments / decrements by 1 to allow multiple things to disable movement at the same time

    public static readonly AsmPatch cameraNoCollideReplacer = new("E8 ?? ?? ?? ?? 45 0F 57 FF", new byte[] { 0x30, 0xC0, 0x90, 0x90, 0x90 }); // E8 ?? ?? ?? ?? 48 8B B4 24 E0 00 00 00 40 32 FF (0x90, 0x90, 0x90, 0x90, 0x90)

    private static AsmPatch addMidHookReplacer;

    public static bool isLoggedIn = false;
    public static bool onLogin = false;
    public static int changingAreaDelay = 0;
    public static bool isChangingAreas = false;

    // Very optimized very good
    public static float GetDefaultLookAtHeightOffset()
    {
        if (CachedDefaultLookAtHeight.HasValue)
            return CachedDefaultLookAtHeight.Value;

        var worldCamera = Common.CameraManager->worldCamera;
        if (worldCamera == null) return 0;

        var prev = worldCamera->lookAtHeightOffset;
        worldCamera->resetLookatHeightOffset = 1;
        ((delegate* unmanaged<GameCamera*, void>)worldCamera->vtbl[3])(worldCamera);
        var ret = worldCamera->lookAtHeightOffset;
        worldCamera->lookAtHeightOffset = prev;
        CachedDefaultLookAtHeight = ret;
        return ret;
    }

    public static void Initialize()
    {
        if (Common.CameraManager == null || Common.CameraManager->worldCamera == null || Common.InputData == null)
            throw new ApplicationException("CameraManager is not initialized!");

        var vtbl = Common.CameraManager->worldCamera->VTable;
        vtbl.setCameraLookAt.CreateHook(SetCameraLookAtDetour);
        vtbl.getCameraPosition.CreateHook(GetCameraPositionDetour);
        vtbl.getCameraTarget.CreateHook(GetCameraTargetDetour);
        vtbl.canChangePerspective.CreateHook(CanChangePerspectiveDetour);
        vtbl.getZoomDelta.CreateHook(GetZoomDeltaDetour);

        GameCamera.getCameraAutoRotateMode.CreateHook(GetCameraAutoRotateModeDetour); // Found inside Client__Game__Camera_UpdateRotation
        GameCamera.getCameraMaxMaintainDistance.CreateHook(GetCameraMaxMaintainDistanceDetour); // Found 1 function deep inside Client__Game__Camera_vf3

        // Gross workaround for fixing legacy control's maintain distance
        var address = DalamudApi.SigScanner.ScanModule("48 85 C9 74 24 48 83 C1 10");
        var offset = BitConverter.GetBytes(GameCamera.getCameraMaxMaintainDistance.Address - (address + 0x8));

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
        if (onLogin)
            onLogin = false;

        if (!isLoggedIn)
            onLogin = isLoggedIn = DalamudApi.ClientState.IsLoggedIn && !DalamudApi.Condition[ConditionFlag.BetweenAreas];

        if (isChangingAreas && !DalamudApi.Condition[ConditionFlag.BetweenAreas] && --changingAreaDelay == 0)
            isChangingAreas = false;
    }

    public static void Dispose() { }
}