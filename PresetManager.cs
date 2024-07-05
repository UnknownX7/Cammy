using System;
using System.Linq;

namespace Cammy;

public static class PresetManager
{
    public static CameraConfigPreset CurrentPreset
    {
        get => PresetOverride ?? ActivePreset ?? DefaultPreset;
        set
        {
            ApplyPreset(PresetOverride = value);
            if (value == null)
                ActivePreset = null;
        }
    }

    public static CameraConfigPreset DefaultPreset { get; set; } = new();

    public static CameraConfigPreset ActivePreset { get; private set; }

    public static CameraConfigPreset PresetOverride { get; private set; }

    public static unsafe void ApplyPreset(CameraConfigPreset preset, bool isLoggingIn = false)
    {
        if (preset == null) return;

        var camera = Common.CameraManager->worldCamera;
        if (camera == null) return;

        if (preset.UseStartZoom && (!preset.UseStartOnLogin || isLoggingIn))
            camera->currentZoom = preset.StartZoom;
        else
            camera->currentZoom = Math.Min(Math.Max(camera->currentZoom, preset.MinZoom), preset.MaxZoom);
        camera->minZoom = preset.MinZoom;
        camera->maxZoom = preset.MaxZoom;

        if (preset.UseStartFoV && (!preset.UseStartOnLogin || isLoggingIn))
            camera->currentFoV = preset.StartFoV;
        else
            camera->currentFoV = Math.Min(Math.Max(camera->currentFoV, preset.MinFoV), preset.MaxFoV);
        camera->minFoV = preset.MinFoV;
        camera->maxFoV = preset.MaxFoV;
        //Game.FoVDelta = preset.FoVDelta;

        camera->minVRotation = preset.MinVRotation;
        camera->maxVRotation = preset.MaxVRotation;

        camera->tilt = preset.Tilt;
        camera->lookAtHeightOffset = preset.LookAtHeightOffset;
    }

    public static void CheckCameraConditionSets(bool isLoggingIn)
    {
        var preset = Cammy.Config.Presets.FirstOrDefault(preset => preset.CheckConditionSet());
        if (preset == null || preset == ActivePreset) return;

        ApplyPreset(preset, isLoggingIn);
        ActivePreset = preset;
    }

    public static void Update()
    {
        if (!DalamudApi.ClientState.IsLoggedIn || FreeCam.Enabled || PresetOverride != null) return;
        CheckCameraConditionSets(false);
    }

    public static void DisableCameraPresets()
    {
        ActivePreset = null;
        PresetOverride = null;
    }
}