using System;
using System.Linq.Expressions;
using Dalamud.Game;
using Dalamud.Plugin;

namespace Cammy
{
    public class Cammy : IDalamudPlugin
    {
        public string Name => "Cammy";
        public static Cammy Plugin { get; private set; }
        public static Configuration Config { get; private set; }

        private CameraEditor camEdit;

        public Cammy(DalamudPluginInterface pluginInterface)
        {
            Plugin = this;
            DalamudApi.Initialize(this, pluginInterface);

            Config = (Configuration)DalamudApi.PluginInterface.GetPluginConfig() ?? new();
            Config.Initialize();

            DalamudApi.Framework.Update += Update;
            DalamudApi.ClientState.Login += Login;
            DalamudApi.ClientState.Logout += Logout;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;

            camEdit = new CameraEditor();
        }

        public void ToggleConfig() => camEdit.editorVisible = !camEdit.editorVisible;

        [Command("/cammy")]
        [HelpMessage("Opens/closes the config.")]
        private void ToggleConfig(string command, string argument) => ToggleConfig();

        public static void PrintEcho(string message) => DalamudApi.ChatGui.Print($"[Cammy] {message}");
        public static void PrintError(string message) => DalamudApi.ChatGui.PrintError($"[Cammy] {message}");

        private void Update(Framework framework) => camEdit?.Update();
        private void Draw() => camEdit?.Draw();
        private void Login(object sender, EventArgs e) => camEdit?.Login();
        private void Logout(object sender, EventArgs e) => camEdit?.Logout();

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Config.Save();

            DalamudApi.Framework.Update -= Update;
            DalamudApi.ClientState.Login -= Login;
            DalamudApi.ClientState.Logout -= Logout;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.Dispose();

            camEdit?.Dispose();
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
