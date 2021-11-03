using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Plugin;

namespace Cammy
{
    public class Cammy : IDalamudPlugin
    {
        public string Name => "Cammy";
        public static Cammy Plugin { get; private set; }
        public static Configuration Config { get; private set; }

        private readonly bool pluginReady = false;

        public Cammy(DalamudPluginInterface pluginInterface)
        {
            try
            {
                Plugin = this;
                DalamudApi.Initialize(this, pluginInterface);

                Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
                Config.Initialize();

                DalamudApi.Framework.Update += Update;
                DalamudApi.ClientState.Logout += Logout;
                DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
                DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
                DalamudApi.ClientState.TerritoryChanged += TerritoryChanged;

                Game.Initialize();
                IPC.Initialize();

                pluginReady = true;
            }
            catch (Exception e) { PluginLog.LogError($"Failed loading plugin\n{e}"); }
        }

        public void ToggleConfig() => PluginUI.isVisible ^= true;

        private const string cammySubcommands = "/cammy [ help | preset | zoom | fov | spectate | nocollide ]";

        [Command("/cammy")]
        [HelpMessage("Opens / closes the config. Additional usage: " + cammySubcommands)]
        private unsafe void ToggleConfig(string command, string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                ToggleConfig();
                return;
            }

            var regex = Regex.Match(argument, "^(\\w+) ?(.*)");
            var subcommand = regex.Success && regex.Groups.Count > 1 ? regex.Groups[1].Value : string.Empty;

            switch (subcommand.ToLower())
            {
                case "preset":
                {
                    if (regex.Groups.Count < 2 || string.IsNullOrEmpty(regex.Groups[2].Value))
                    {
                        PresetManager.SetPresetOverride(null);
                        PrintEcho("Removed preset override.");
                        return;
                    }

                    var arg = regex.Groups[2].Value;
                    var preset = Config.Presets.FirstOrDefault(preset => preset.Name == arg);

                    if (preset == null)
                    {
                        PrintError($"Failed to find preset \"{arg}\"");
                        return;
                    }

                    PresetManager.SetPresetOverride(preset);
                    PrintEcho($"Preset set to \"{arg}\"");
                    break;
                }
                case "zoom":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        PrintError("Invalid amount.");
                        return;
                    }

                    Game.cameraManager->WorldCamera->CurrentZoom = amount;
                    break;
                }
                case "fov":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        PrintError("Invalid amount.");
                        return;
                    }

                    Game.cameraManager->WorldCamera->CurrentFoV = amount;
                    break;
                }
                case "spectate":
                {
                    if (!Game.GetCameraTargetHook.IsEnabled)
                        Game.GetCameraTargetHook.Enable();
                    else
                        Game.GetCameraTargetHook.Disable();

                    PrintEcho($"Spectating is now {(Game.GetCameraTargetHook.IsEnabled ? "enabled" : "disabled")}!");
                    break;
                }
                case "nocollide":
                {
                    Game.cameraNoCollideReplacer.Toggle();
                    PrintEcho($"Camera collision is now {(Game.cameraNoCollideReplacer.IsEnabled ? "disabled" : "enabled")}!");
                    break;
                }
                case "help":
                {
                    PrintEcho("Subcommands:" +
                        "\npreset <name> - Applies a preset to override automatic presets, specified by name. Use without a name to disable." +
                        "\nzoom <amount> - Sets the current zoom level." +
                        "\nfov <amount> - Sets the current FoV level." +
                        "\nspectate - Toggles the \"Spectate Focus / Soft Target\" option." +
                        "\nnocollide - Toggles the \"Disable Camera Collision\" option.");
                    break;
                }
                default:
                {
                    PrintError("Invalid usage: " + cammySubcommands);
                    break;
                }
            }
        }

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[Cammy] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[Cammy] {message}");

        private void Update(Framework framework)
        {
            if (!pluginReady) return;
            Game.Update();
            PresetManager.Update();
        }

        private void Draw()
        {
            if (!pluginReady) return;
            PluginUI.Draw();
        }

        private void Logout(object sender, EventArgs e)
        {
            if (!pluginReady) return;
            Game.isLoggedIn = false;
            PresetManager.DisableCameraPresets();
        }
        private void TerritoryChanged(object sender, ushort id)
        {
            if (!pluginReady) return;
            Game.isChangingAreas = true;
            Game.changingAreaDelay = 1;
            PresetManager.DisableCameraPresets();
        }
        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            IPC.Dispose();

            Config.Save();
            new CameraConfigPreset().Apply();

            DalamudApi.Framework.Update -= Update;
            DalamudApi.ClientState.Logout -= Logout;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.Dispose();

            Game.Dispose();
            Memory.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public static class Extensions
    {
        public static object Cast(this Type Type, object data)
        {
            var DataParam = Expression.Parameter(typeof(object), "data");
            var Body = Expression.Block(Expression.Convert(Expression.Convert(DataParam, data.GetType()), Type));

            var Run = Expression.Lambda(Body, DataParam).Compile();
            var ret = Run.DynamicInvoke(data);
            return ret;
        }
    }
}
