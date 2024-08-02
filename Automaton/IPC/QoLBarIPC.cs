using ECommons.EzIpcManager;

namespace Automaton.IPC;

#nullable disable
internal class QoLBarIPC
{
    public static string Name = "QoLBar";
    public QoLBarIPC() => EzIPC.Init(this, Name);
    public static bool IsEnabled => Utils.HasPlugin(Name);

    [EzIPC] public readonly Func<string> GetVersion;
    [EzIPC] public readonly Func<int> GetIPCVersion;
    [EzIPC] public readonly Func<string, object> ImportBar;
    [EzIPC] public readonly Func<string[]> GetConditionSets;
    [EzIPC] public readonly Func<int, bool> CheckConditionSet;
    [EzIPC] public readonly Func<int, int, object> MovedConditionSet;
    [EzIPC] public readonly Func<int, object> RemovedConditionSet;
}
