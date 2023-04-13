using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Plugin;

namespace Cammy;

public class Cammy : DalamudPlugin<Cammy, Configuration>, IDalamudPlugin
{
    public override string Name => "Cammy";

    public Cammy(DalamudPluginInterface pluginInterface) : base(pluginInterface) { }

    protected override void Initialize()
    {
        Game.Initialize();
        IPC.Initialize();

        DalamudApi.ClientState.Login += Login;
        DalamudApi.ClientState.Logout += Logout;
        DalamudApi.ClientState.TerritoryChanged += TerritoryChanged;
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    private const string cammySubcommands = "/cammy [ help | preset | zoom | fov | spectate | nocollide | freecam ]";

    [PluginCommand("/cammy", HelpMessage = "Opens / closes the config. Additional usage: " + cammySubcommands)]
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
                        PresetManager.CurrentPreset = null;
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

                    PresetManager.CurrentPreset = preset;
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

                    Common.CameraManager->worldCamera->currentZoom = amount;
                    break;
                }
            case "fov":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        PrintError("Invalid amount.");
                        return;
                    }

                    Common.CameraManager->worldCamera->currentFoV = amount;
                    break;
                }
            case "spectate":
                {
                    Game.EnableSpectating ^= true;
                    PrintEcho($"Spectating is now {(Game.EnableSpectating ? "enabled" : "disabled")}!");
                    break;
                }
            case "nocollide":
                {
                    Game.cameraNoCollideReplacer.Toggle();
                    PrintEcho($"Camera collision is now {(Game.cameraNoCollideReplacer.IsEnabled ? "disabled" : "enabled")}!");
                    break;
                }
            case "freecam":
                {
                    FreeCam.Toggle();
                    break;
                }
            case "help":
                {
                    PrintEcho("Subcommands:" +
                        "\npreset <name> - Applies a preset to override automatic presets, specified by name. Use without a name to disable." +
                        "\nzoom <amount> - Sets the current zoom level." +
                        "\nfov <amount> - Sets the current FoV level." +
                        "\nspectate - Toggles the \"Spectate Focus / Soft Target\" option." +
                        "\nnocollide - Toggles the \"Disable Camera Collision\" option." +
                        "\nfreecam - Toggles the \"Free Cam\" option.");
                    break;
                }
            default:
                {
                    PrintError("Invalid usage: " + cammySubcommands);
                    break;
                }
        }
    }

    protected override void Update()
    {
        Game.Update();
        FreeCam.Update();
        PresetManager.Update();
    }

    protected override void Draw() => PluginUI.Draw();

    private void Login(object sender, EventArgs e) => Game.CachedDefaultLookAtHeight = null;

    private void Logout(object sender, EventArgs e)
    {
        Game.CachedDefaultLookAtHeight = null;
        Game.isLoggedIn = false;
        PresetManager.DisableCameraPresets();
    }

    private void TerritoryChanged(object sender, ushort id)
    {
        Game.isChangingAreas = true;
        Game.changingAreaDelay = 1;
        PresetManager.DisableCameraPresets();
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        IPC.Dispose();
        PresetManager.DefaultPreset.Apply();
        DalamudApi.ClientState.Login -= Login;
        DalamudApi.ClientState.Logout -= Logout;
        DalamudApi.ClientState.TerritoryChanged -= TerritoryChanged;

        if (FreeCam.Enabled)
            FreeCam.Toggle();

        Game.Dispose();
    }
}