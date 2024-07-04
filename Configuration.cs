using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dalamud.Configuration;

namespace Cammy;

public class CameraConfigPreset
{
    public enum ViewBobSetting
    {
        Disabled,
        [Display(Name = "First Person")] FirstPerson,
        [Display(Name = "Out of Combat")] OutOfCombat,
        Always
    }

    public string Name = "New Preset";

    public bool UseStartOnLogin = false;

    public bool UseStartZoom = false;
    public float StartZoom = 6;
    public float MinZoom = 1.5f;
    public float MaxZoom = 20;
    public float ZoomDelta = 0.75f;

    public bool UseStartFoV = false;
    public float StartFoV = 0.78f;
    public float MinFoV = 0.69f;
    public float MaxFoV = 0.78f;
    public float FoVDelta = 0.08726646751f;

    public float MinVRotation = -1.483530f;
    public float MaxVRotation = 0.785398f;

    public float HeightOffset = 0;
    public float SideOffset = 0;
    public float Tilt = 0;
    public float LookAtHeightOffset = Game.GetDefaultLookAtHeightOffset() ?? 0;
    public ViewBobSetting ViewBobMode = ViewBobSetting.Disabled;
    public int ConditionSet = -1;

    public CameraConfigPreset Clone() => (CameraConfigPreset)MemberwiseClone();

    public bool CheckConditionSet() => ConditionSet < 0 || IPC.QoLBarEnabled && IPC.CheckConditionSet(ConditionSet);

    public void Apply(bool isLoggingIn = false) => PresetManager.ApplyPreset(this, isLoggingIn);
}

public class Configuration : PluginConfiguration, IPluginConfiguration
{
    public enum DeathCamSetting
    {
        Disabled,
        Spectate,
        [Display(Name = "Free Cam")] FreeCam
    }

    public int Version { get; set; }

    public List<CameraConfigPreset> Presets = [];
    public bool EnableCameraNoClippy = false;
    public DeathCamSetting DeathCamMode = DeathCamSetting.Disabled;
}