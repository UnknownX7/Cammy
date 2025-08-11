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
    public static bool IsSpectating { get; private set; } = false;

    // xor al, al
    public static readonly AsmPatch cameraNoClippyReplacer = new("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? F3 0F 10 44 24 ?? 41 B7 01", [ 0x30, 0xC0, 0x90, 0x90, 0x90 ], Cammy.Config.EnableCameraNoClippy);
    private static AsmPatch addMidHookReplacer;

    [HypostasisSignatureInjection("F3 0F 59 35 ?? ?? ?? ?? F3 0F 10 45 ??", Static = true, Required = true)]
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

    [HypostasisSignatureInjection("F3 0F 10 05 ?? ?? ?? ?? 0F 2E C7", Offset = 4, Static = true, Required = true)] // Also found at g_PlayerMoveController + 0x54C?
    private static nint forceDisableMovementPtr;
    public static ref int ForceDisableMovement => ref *(int*)forceDisableMovementPtr; // Increments / decrements by 1 to allow multiple things to disable movement at the same time

    private static float GetZoomDeltaDetour() => PresetManager.CurrentPreset.ZoomDelta;

    private static void SetCameraLookAtDetour(GameCamera* camera, Vector3* lookAtPosition, Vector3* cameraPosition, Vector3* a4) // a4 seems to be immediately overwritten and unused
    {
        if (FreeCam.Enabled) return;
        camera->VTable.setCameraLookAt.Original(camera, lookAtPosition, cameraPosition, a4);
    }

    private static float cachedDefaultLookAtHeightOffset;
    private static GameObject* prevCameraTarget;
    private static Vector3 prevCameraTargetPosition;
    private static float interpolatedHeight;
    private static void GetCameraPositionDetour(GameCamera* camera, GameObject* target, Vector3* position, Bool swapPerson)
    {
        if (!FreeCam.Enabled)
        {
            var preset = PresetManager.CurrentPreset;

            camera->VTable.getCameraPosition.Original(camera, target, position, swapPerson);

            if (((preset.ViewBobMode == CameraConfigPreset.ViewBobSetting.FirstPerson && (camera->mode == 0 || (camera->transition != 0 && camera->controlType <= 2)))
                    || (preset.ViewBobMode == CameraConfigPreset.ViewBobSetting.OutOfCombat && !DalamudApi.Condition[ConditionFlag.InCombat])
                    || preset.ViewBobMode == CameraConfigPreset.ViewBobSetting.Always)
                && Common.getWorldBonePosition.IsValid && target->DrawObject != null)
            {
                // Data seems to be cached somehow and the position is slightly behind, but only at this point in the frame
                if (target != prevCameraTarget)
                    prevCameraTargetPosition = target->Position;

                var newPos = Common.GetBoneWorldPosition(target, 26) + ((Vector3)target->Position - prevCameraTargetPosition);
                var d = target->Position.Y - interpolatedHeight;
                if (target == prevCameraTarget && d is > -3 and < 3)
                {
                    var amount = d * (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * 10;
                    interpolatedHeight = d >= 0
                        ? Math.Max(interpolatedHeight + Math.Min(Math.Max(amount, 0), d), target->Position.Y - 0.7f)
                        : Math.Min(interpolatedHeight + Math.Max(Math.Min(amount, 0), d), target->Position.Y + 0.7f);
                }
                else
                {
                    interpolatedHeight = target->Position.Y;
                }

                *position = newPos with { Y = newPos.Y - (target->Position.Y - interpolatedHeight) };
                prevCameraTarget = target;
                prevCameraTargetPosition = target->Position;
            }
            else
            {
                if (preset.ViewBobMode != CameraConfigPreset.ViewBobSetting.Disabled && (nint)target == DalamudApi.ClientState.LocalPlayer?.Address)
                {
                    var defaultLookAtHeightOffset = GetDefaultLookAtHeightOffset();
                    if (defaultLookAtHeightOffset.HasValue)
                        cachedDefaultLookAtHeightOffset = defaultLookAtHeightOffset.Value;
                    position->Y += cachedDefaultLookAtHeightOffset;
                }

                prevCameraTarget = null;
            }

            position->Y += preset.HeightOffset;

            if (preset.SideOffset == 0 || camera->mode != 1) return;

            const float halfPI = MathF.PI / 2f;
            var a = Common.CameraManager->worldCamera->currentHRotation - halfPI;
            position->X += -preset.SideOffset * MathF.Sin(a);
            position->Z += -preset.SideOffset * MathF.Cos(a);
        }
        else
        {
            *position = FreeCam.Position;
        }
    }

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

        if (Cammy.Config.DeathCamMode == Configuration.DeathCamSetting.Spectate && DalamudApi.Condition[ConditionFlag.Unconscious] && DalamudApi.TargetManager.Target is { } target)
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

    public static float? GetDefaultLookAtHeightOffset()
    {
        var worldCamera = Common.CameraManager->worldCamera;
        var p = GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        if (worldCamera == null || p == null) return 0;

        var prev = worldCamera->lookAtHeightOffset;
        if (!GameCamera.updateLookAtHeightOffset.Original(worldCamera, p, false)) return null;

        var ret = worldCamera->lookAtHeightOffset;
        worldCamera->lookAtHeightOffset = prev;
        return ret;
    }

    public static Bool UpdateLookAtHeightOffsetDetour(GameCamera* camera, GameObject* o, Bool zero)
    {
        var ret = GameCamera.updateLookAtHeightOffset.Original(camera, o, zero);
        if (ret && !zero && (nint)o == DalamudApi.ClientState.LocalPlayer?.Address && PresetManager.CurrentPreset != PresetManager.DefaultPreset)
            camera->lookAtHeightOffset = PresetManager.CurrentPreset.LookAtHeightOffset;
        return ret;
    }

    public static Bool ShouldDisplayObjectDetour(GameCamera* camera, GameObject* o, Vector3* cameraPosition, Vector3* cameraLookAt) =>
        ((nint)o != DalamudApi.ClientState.LocalPlayer?.Address || camera != Common.CameraManager->worldCamera || camera->mode != 0 || (camera->transition != 0 && camera->controlType <= 2)) && GameCamera.shouldDisplayObject.Original(camera, o, cameraPosition, cameraLookAt);

    public static void Initialize()
    {
        if (Common.CameraManager == null || !Common.IsValid(Common.CameraManager->worldCamera) || !Common.IsValid(Common.InputData))
            throw new ApplicationException("Failed to validate core structures!");

        var vtbl = Common.CameraManager->worldCamera->VTable;
        vtbl.setCameraLookAt.CreateHook(SetCameraLookAtDetour);
        vtbl.getCameraPosition.CreateHook(GetCameraPositionDetour);
        vtbl.getCameraTarget.CreateHook(GetCameraTargetDetour);
        vtbl.canChangePerspective.CreateHook(CanChangePerspectiveDetour);
        vtbl.getZoomDelta.CreateHook(GetZoomDeltaDetour);

        GameCamera.getCameraAutoRotateMode.CreateHook(GetCameraAutoRotateModeDetour);
        GameCamera.getCameraMaxMaintainDistance.CreateHook(GetCameraMaxMaintainDistanceDetour);
        GameCamera.updateLookAtHeightOffset.CreateHook(UpdateLookAtHeightOffsetDetour);
        GameCamera.shouldDisplayObject.CreateHook(ShouldDisplayObjectDetour);

        // Gross workaround for fixing legacy control's maintain distance
        /*var address = DalamudApi.SigScanner.ScanModule(""); // F3 0F 5D F2 48 85 D2
        var offset = BitConverter.GetBytes(GameCamera.getCameraMaxMaintainDistance.Address - (address + 0x8));

        // mov rcx, rbx
        // call offset
        // jmp 27h
        addMidHookReplacer = new(address,
            [
                    0x48, 0x8B, 0xCB,
                    0xE8, offset[0], offset[1], offset[2], offset[3],
                    0xEB, 0x27,
                    0x90, 0x90, 0x90, 0x90
            ],
            true);*/
    }

    public static void Dispose() { }
}