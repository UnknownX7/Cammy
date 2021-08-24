using Dalamud.Configuration;

namespace Cammy
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool AutoLoadCameraPreset = false;
        public CameraEditor.CameraPreset CameraPreset = new();

        public void Initialize() { }

        public void Save() => DalamudApi.PluginInterface.SavePluginConfig(this);
    }
}
