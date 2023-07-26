using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
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
    }

    protected override void ToggleConfig() => PluginUI.IsVisible ^= true;

    private const string cammySubcommands = "/cammy [ help | preset | zoom | fov | spectate | nocollide | freecam ]";

    [PluginCommand("/cammy", HelpMessage = "打开/ 关闭设置. 附加指令: " + cammySubcommands)]
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
            case "预设":
                {
                    if (regex.Groups.Count < 2 || string.IsNullOrEmpty(regex.Groups[2].Value))
                    {
                        PresetManager.CurrentPreset = null;
                        PrintEcho("删除了预设覆盖.");
                        return;
                    }

                    var arg = regex.Groups[2].Value;
                    var preset = Config.Presets.FirstOrDefault(preset => preset.Name == arg);

                    if (preset == null)
                    {
                        PrintError($"找不到预设 \"{arg}\"");
                        return;
                    }

                    PresetManager.CurrentPreset = preset;
                    PrintEcho($"预设设置为 \"{arg}\"");
                    break;
                }
            case "zoom":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        PrintError("无效值.");
                        return;
                    }

                    Common.CameraManager->worldCamera->currentZoom = amount;
                    break;
                }
            case "fov":
                {
                    if (regex.Groups.Count < 2 || !float.TryParse(regex.Groups[2].Value, out var amount))
                    {
                        PrintError("无效值.");
                        return;
                    }

                    Common.CameraManager->worldCamera->currentFoV = amount;
                    break;
                }
            case "查看":
                {
                    Game.EnableSpectating ^= true;
                    PrintEcho($"查看 {(Game.EnableSpectating ? "启用" : "未启用")}!");
                    break;
                }
            case "镜头碰撞":
                {
                    Config.EnableCameraNoClippy ^= true;
                    if (!FreeCam.Enabled)
                        Game.cameraNoClippyReplacer.Toggle();
                    Config.Save();
                    PrintEcho($"镜头模型碰撞 {(Config.EnableCameraNoClippy ? "未启用" : "启用")}!");
                    break;
                }
            case "自由镜头":
                {
                    FreeCam.Toggle();
                    break;
                }
            case "帮助":
                {
                    PrintEcho("子命令：" +
                        "\npreset <name> - 应用预设来覆盖按名称指定的自动预设。不使用名称即可禁用。" +
                        "\nzoom <amount> - 设置当前视距值." +
                        "\nfov <amount> - 设置当前视野值." +
                        "\nspectate - 切换“观看焦点/软目标”选项。" +
                        "\nnocollide - 切换“禁用镜头模型碰撞”选项。" +
                        "\nfreecam - 切换“自由镜头”选项。");
                    break;
                }
            default:
                {
                    PrintError("无效使用: " + cammySubcommands);
                    break;
                }
        }
    }

    protected override void Update()
    {
        FreeCam.Update();
        PresetManager.Update();
    }

    protected override void Draw() => PluginUI.Draw();

    private static void Login(object sender, EventArgs e)
    {
        DalamudApi.Framework.Update += UpdateDefaultPreset;
        PresetManager.DisableCameraPresets();
        PresetManager.CheckCameraConditionSets(true);
    }

    private static void UpdateDefaultPreset(Framework framework)
    {
        if (DalamudApi.Condition[ConditionFlag.BetweenAreas]) return;
        PresetManager.DefaultPreset = new();
        DalamudApi.Framework.Update -= UpdateDefaultPreset;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        IPC.Dispose();
        PresetManager.DefaultPreset.Apply();
        DalamudApi.ClientState.Login -= Login;

        if (FreeCam.Enabled)
            FreeCam.Toggle();

        Game.Dispose();
    }
}