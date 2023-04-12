using System;
using Dalamud.Plugin.Ipc;

namespace Cammy;

public static class IPC
{
    public static bool QoLBarEnabled { get; private set; } = false;
    public static ICallGateSubscriber<object> qolBarInitializedSubscriber;
    public static ICallGateSubscriber<object> qolBarDisposedSubscriber;
    public static ICallGateSubscriber<string> qolBarGetVersionSubscriber;
    public static ICallGateSubscriber<int> qolBarGetIPCVersionSubscriber;
    public static ICallGateSubscriber<string[]> qolBarGetConditionSetsProvider;
    public static ICallGateSubscriber<int, bool> qolBarCheckConditionSetProvider;
    public static ICallGateSubscriber<int, int, object> qolBarMovedConditionSetProvider;
    public static ICallGateSubscriber<int, object> qolBarRemovedConditionSetProvider;

    public static int QoLBarIPCVersion
    {
        get
        {
            try { return qolBarGetIPCVersionSubscriber.InvokeFunc(); }
            catch { return 0; }
        }
    }

    public static string QoLBarVersion
    {
        get
        {
            try { return qolBarGetVersionSubscriber.InvokeFunc(); }
            catch { return "0.0.0.0"; }
        }
    }

    public static string[] QoLBarConditionSets
    {
        get
        {
            try { return qolBarGetConditionSetsProvider.InvokeFunc(); }
            catch { return Array.Empty<string>(); }
        }
    }

    public static void Initialize()
    {
        qolBarInitializedSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<object>("QoLBar.Initialized");
        qolBarDisposedSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<object>("QoLBar.Disposed");
        qolBarGetIPCVersionSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<int>("QoLBar.GetIPCVersion");
        qolBarGetVersionSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<string>("QoLBar.GetVersion");
        qolBarGetConditionSetsProvider = DalamudApi.PluginInterface.GetIpcSubscriber<string[]>("QoLBar.GetConditionSets");
        qolBarCheckConditionSetProvider = DalamudApi.PluginInterface.GetIpcSubscriber<int, bool>("QoLBar.CheckConditionSet");
        qolBarMovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcSubscriber<int, int, object>("QoLBar.MovedConditionSet");
        qolBarRemovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcSubscriber<int, object>("QoLBar.RemovedConditionSet");

        qolBarInitializedSubscriber.Subscribe(EnableQoLBarIPC);
        qolBarDisposedSubscriber.Subscribe(DisableQoLBarIPC);
        qolBarMovedConditionSetProvider.Subscribe(OnMovedConditionSet);
        qolBarRemovedConditionSetProvider.Subscribe(OnRemovedConditionSet);

        EnableQoLBarIPC();
    }

    public static void EnableQoLBarIPC()
    {
        if (QoLBarIPCVersion != 1) return;
        QoLBarEnabled = true;
    }

    public static void DisableQoLBarIPC()
    {
        if (!QoLBarEnabled) return;
        QoLBarEnabled = false;
    }

    public static bool CheckConditionSet(int i)
    {
        try { return qolBarCheckConditionSetProvider.InvokeFunc(i); }
        catch { return false; }
    }

    private static void OnMovedConditionSet(int from, int to)
    {
        foreach (var preset in Cammy.Config.Presets)
        {
            if (preset.ConditionSet == from)
                preset.ConditionSet = to;
            else if (preset.ConditionSet == to)
                preset.ConditionSet = from;
        }
        Cammy.Config.Save();
    }

    private static void OnRemovedConditionSet(int removed)
    {
        foreach (var preset in Cammy.Config.Presets)
        {
            if (preset.ConditionSet > removed)
                preset.ConditionSet -= 1;
            else if (preset.ConditionSet == removed)
                preset.ConditionSet = -1;
        }
        Cammy.Config.Save();
    }

    public static void Dispose()
    {
        qolBarInitializedSubscriber?.Unsubscribe(EnableQoLBarIPC);
        qolBarDisposedSubscriber?.Unsubscribe(DisableQoLBarIPC);
        qolBarMovedConditionSetProvider?.Unsubscribe(OnMovedConditionSet);
        qolBarRemovedConditionSetProvider?.Unsubscribe(OnRemovedConditionSet);
        QoLBarEnabled = false;
    }
}