using System;
using System.Reflection;
using System.Linq.Expressions;
using Dalamud.Plugin;
using Cammy.Attributes;

[assembly: AssemblyTitle("Cammy")]
[assembly: AssemblyVersion("1.0.2.0")]

namespace Cammy
{
    public class Cammy : IDalamudPlugin
    {
        public static DalamudPluginInterface Interface { get; private set; }
        private PluginCommandManager<Cammy> commandManager;
        public static Configuration Config { get; private set; }
        public static Cammy Plugin { get; private set; }

        private CameraEditor camEdit;

        public string Name => "Cammy";

        public void Initialize(DalamudPluginInterface p)
        {
            Plugin = this;
            Interface = p;

            Config = (Configuration)Interface.GetPluginConfig() ?? new Configuration();
            Config.Initialize(Interface);

            Interface.ClientState.OnLogin += OnLogin;
            Interface.ClientState.OnLogout += OnLogout;
            Interface.UiBuilder.OnOpenConfigUi += ToggleConfig;
            Interface.UiBuilder.OnBuildUi += Draw;

            commandManager = new PluginCommandManager<Cammy>(this, Interface);

            camEdit = new CameraEditor();
        }

        public void ToggleConfig(object sender, EventArgs e) => ToggleConfig();

        [Command("/cammy")]
        [HelpMessage("Opens/closes the config.")]
        private void ToggleConfig(string command = null, string argument = null) => camEdit.editorVisible = !camEdit.editorVisible;

        public static void PrintEcho(string message) => Interface.Framework.Gui.Chat.Print($"[Cammy] {message}");
        public static void PrintError(string message) => Interface.Framework.Gui.Chat.PrintError($"[Cammy] {message}");

        private void Draw() => camEdit?.Draw();
        private void OnLogin(object sender, EventArgs e) => camEdit?.OnLogin();
        private void OnLogout(object sender, EventArgs e) => camEdit?.OnLogout();

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            commandManager.Dispose();

            camEdit?.Dispose();
            Memory.Dispose();

            Interface.SavePluginConfig(Config);

            Interface.ClientState.OnLogin -= OnLogin;
            Interface.ClientState.OnLogout -= OnLogout;
            Interface.UiBuilder.OnOpenConfigUi -= ToggleConfig;
            Interface.UiBuilder.OnBuildUi -= Draw;

            Interface.Dispose();
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
