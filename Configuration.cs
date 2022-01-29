using System;
using System.Collections.Generic;
using Dalamud.Configuration;

#pragma warning disable CS0612 // Type or member is obsolete

namespace Cammy
{
    public class CameraConfigPreset
    {
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
        public float AddedFoV = 0;

        public float MinVRotation = -1.483530f;
        public float MaxVRotation = 0.785398f;

        public float HeightOffset = 0;
        public float SideOffset = 0;
        public float Tilt = 0;
        public float LookAtHeightOffset = Game.GetDefaultLookAtHeightOffset();
        public int ConditionSet = -1;

        public CameraConfigPreset Clone() => (CameraConfigPreset)MemberwiseClone();

        public bool CheckConditionSet() => ConditionSet < 0 || IPC.QoLBarEnabled && IPC.CheckConditionSet(ConditionSet);

        public unsafe void Apply()
        {
            if (Game.cameraManager == null) return;

            var camera = Game.cameraManager->WorldCamera;

            if (camera == null) return;

            if (UseStartZoom && (!UseStartOnLogin || Game.onLogin))
                camera->CurrentZoom = StartZoom;
            else
                camera->CurrentZoom = Math.Min(Math.Max(camera->CurrentZoom, MinZoom), MaxZoom);
            camera->MinZoom = MinZoom;
            camera->MaxZoom = MaxZoom;
            Game.zoomDelta = ZoomDelta;

            if (UseStartFoV && (!UseStartOnLogin || Game.onLogin))
                camera->CurrentFoV = StartFoV;
            else
                camera->CurrentFoV = Math.Min(Math.Max(camera->CurrentFoV, MinFoV), MaxFoV);
            camera->MinFoV = MinFoV;
            camera->MaxFoV = MaxFoV;
            Game.FoVDelta = FoVDelta;

            camera->MinVRotation = MinVRotation;
            camera->MaxVRotation = MaxVRotation;

            Game.cameraHeightOffset = HeightOffset;
            Game.cameraSideOffset = SideOffset;
            camera->Tilt = Tilt;
            camera->LookAtHeightOffset = LookAtHeightOffset;
        }
    }

    // LEGACY (To be removed in EW)
    public class CameraEditor
    {
        public class CameraPreset
        {
            public float CurrentZoom = 6f;
            public float MinZoom = 1.5f;
            public float MaxZoom = 20f;
            public float ZoomDelta = 0.75f;

            public float CurrentFoV = 0.78f;
            public float MinFoV = 0.69f;
            public float MaxFoV = 0.78f;
            public float FoVDelta = 0.08726646751f;
            public float AddedFoV = 0f;

            public float CurrentVRotation = -0.349066f;
            public float MinVRotation = -1.483530f;
            public float MaxVRotation = 0.785398f;

            public float CenterHeightOffset = 0f;
        }
    }

    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        [Obsolete] public CameraEditor.CameraPreset CameraPreset { internal get; set; }
        public List<CameraConfigPreset> Presets = new();
        [Obsolete] public bool FreeCamOnDeath { internal get; set; }
        public int DeathCamMode = 0;

        public void Initialize()
        {
            if (FreeCamOnDeath)
            {
                DeathCamMode = 2;
                Save();
            }

            if (CameraPreset == null) return;

            Presets.Add(new()
            {
                Name = "Imported Config",
                UseStartZoom = true,
                StartZoom = CameraPreset.CurrentZoom,
                MinZoom = CameraPreset.MinZoom,
                MaxZoom = CameraPreset.MaxZoom,
                ZoomDelta = CameraPreset.ZoomDelta,
                UseStartFoV = true,
                StartFoV = CameraPreset.CurrentFoV,
                MinFoV = CameraPreset.MinFoV,
                MaxFoV = CameraPreset.MaxFoV,
                FoVDelta = CameraPreset.FoVDelta,
                AddedFoV = CameraPreset.AddedFoV,
                MinVRotation = CameraPreset.MinVRotation,
                MaxVRotation = CameraPreset.MaxVRotation,
                LookAtHeightOffset = CameraPreset.CenterHeightOffset
            });

            Save();
        }

        public void Save() => DalamudApi.PluginInterface.SavePluginConfig(this);
    }
}
