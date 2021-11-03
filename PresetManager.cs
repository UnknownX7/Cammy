using System.Linq;

namespace Cammy
{
    public static class PresetManager
    {
        public static CameraConfigPreset activePreset;
        public static CameraConfigPreset presetOverride;

        public static void CheckCameraConditionSets()
        {
            var preset = Cammy.Config.Presets.FirstOrDefault(preset => preset.CheckConditionSet());
            if (preset == null || preset == activePreset) return;

            DisableCameraPresets();
            preset.Apply();
            activePreset = preset;
        }

        public static void DisableCameraPresets()
        {
            activePreset = null;
        }

        public static void SetPresetOverride(CameraConfigPreset preset)
        {
            DisableCameraPresets();
            presetOverride = preset;
        }

        public static void Update()
        {
            if (!Game.isLoggedIn || Game.isChangingAreas || Game.IsFreeCamEnabled) return;

            if (presetOverride != null)
            {
                if (activePreset != null) return;
                activePreset = presetOverride;
                activePreset.Apply();
                return;
            }

            CheckCameraConditionSets();
        }
    }
}