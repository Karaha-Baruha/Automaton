using ECommons.EzIpcManager;

namespace Automaton.IPC;

#nullable disable
public class AutoRetainerIPC
{
    public const string Name = "AutoRetainer";
    public const string Repo = "https://love.puni.sh/ment.json";
    public AutoRetainerIPC() => EzIPC.Init(this, "AutoRetainer.PluginState");
    public static bool Installed => Utils.HasPlugin(Name);

    [EzIPC] public readonly Func<bool> IsBusy;
    [EzIPC] public readonly Func<int> GetInventoryFreeSlotCount;
}
