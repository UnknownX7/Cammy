using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace Cammy
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool FixFoVBug = false;
        public bool AutoLoadCameraPreset = false;
        public CameraEditor.CameraPreset CameraPreset = new CameraEditor.CameraPreset();

        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
