using ECommons.EzIpcManager;

namespace SomethingNeedDoing.IPC;

#nullable disable
public class DropboxIPC
{
    public const string Name = "Dropbox";
    public const string Repo = "https://puni.sh/api/repository/kawaii";
    public DropboxIPC() => EzIPC.Init(this, Name);
    public static bool IsEnabled => Utils.HasPlugin(Name);

    [EzIPC] public readonly Func<bool> IsBusy;
    [EzIPC] public readonly Func<uint, bool, int> GetItemQuantity; // id, hq
    [EzIPC] public readonly Action<uint, bool, int> SetItemQuantity; // id, hq, quantity

    [EzIPC] public readonly Action BeginTradingQueue;
    [EzIPC] public readonly Action Stop;
}
