using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Runtime.InteropServices;

namespace Automaton.Utilities;
internal unsafe class Memory
{
    public unsafe delegate void RidePillionDelegate(BattleChara* target, int seatIndex);
    public RidePillionDelegate? RidePillion = null!;

    public unsafe delegate void SalvageItemDelegate(AgentSalvage* thisPtr, InventoryItem* item, int addonId, byte a4);
    public SalvageItemDelegate SalvageItem = null!;

    public delegate void AbandonDutyDelegate(bool a1);
    public AbandonDutyDelegate AbandonDuty = null!;

    public Memory()
    {
        EzSignatureHelper.Initialize(this);
        RidePillion = Marshal.GetDelegateForFunctionPointer<RidePillionDelegate>(Svc.SigScanner.ScanText("48 85 C9 0F 84 ?? ?? ?? ?? 48 89 6C 24 ?? 56 48 83 EC"));
        SalvageItem = Marshal.GetDelegateForFunctionPointer<SalvageItemDelegate>(Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 46 48 8B 03")); // thanks veyn
        AbandonDuty = Marshal.GetDelegateForFunctionPointer<AbandonDutyDelegate>(Svc.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 43 28 41 B2 01"));
    }

    #region PacketDispatcher
    const string PacketDispatcher_OnReceivePacketHookSig = "40 53 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 8B F2";
    public delegate void PacketDispatcher_OnReceivePacket(nint a1, uint a2, nint a3);
    [EzHook(PacketDispatcher_OnReceivePacketHookSig, false)]
    public EzHook<PacketDispatcher_OnReceivePacket> PacketDispatcher_OnReceivePacketHook = null!;
    [EzHook(PacketDispatcher_OnReceivePacketHookSig, false)]
    public EzHook<PacketDispatcher_OnReceivePacket> PacketDispatcher_OnReceivePacketMonitorHook = null!;

    internal delegate byte PacketDispatcher_OnSendPacket(nint a1, nint a2, nint a3, byte a4);
    [EzHook("48 89 5C 24 ?? 48 89 74 24 ?? 4C 89 64 24 ?? 55 41 56 41 57 48 8B EC 48 83 EC 70", false)]
    internal EzHook<PacketDispatcher_OnSendPacket> PacketDispatcher_OnSendPacketHook = null!;

    public List<uint> DisallowedSentPackets = [];
    public List<uint> DisallowedReceivedPackets = [];

    private byte PacketDispatcher_OnSendPacketDetour(nint a1, nint a2, nint a3, byte a4)
    {
        const byte DefaultReturnValue = 1;

        if (a2 == IntPtr.Zero)
        {
            PluginLog.Error("[HyperFirewall] Error: Opcode pointer is null.");
            return DefaultReturnValue;
        }

        try
        {
            Events.OnPacketSent(a1, a2, a3, a4);
            var opcode = *(ushort*)a2;

            if (DisallowedSentPackets.Contains(opcode))
            {
                PluginLog.Verbose($"[HyperFirewall] Suppressing outgoing packet with opcode {opcode}.");
            }
            else
            {
                PluginLog.Verbose($"[HyperFirewall] Passing outgoing packet with opcode {opcode} through.");
                return PacketDispatcher_OnSendPacketHook.Original(a1, a2, a3, a4);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperFirewall] Exception caught while processing opcode: {e.Message}");
            e.Log();
            return DefaultReturnValue;
        }

        return DefaultReturnValue;
    }

    private void PacketDispatcher_OnReceivePacketDetour(nint a1, uint a2, nint a3)
    {
        if (a3 == IntPtr.Zero)
        {
            PluginLog.Error("[HyperFirewall] Error: Data pointer is null.");
            return;
        }

        try
        {
            Events.OnPacketRecieved(a1, a2, a3);
            var opcode = *(ushort*)(a3 + 2);

            if (DisallowedReceivedPackets.Contains(opcode))
            {
                PluginLog.Verbose($"[HyperFirewall] Suppressing incoming packet with opcode {opcode}.");
            }
            else
            {
                PluginLog.Verbose($"[HyperFirewall] Passing incoming packet with opcode {opcode} through.");
                PacketDispatcher_OnReceivePacketHook.Original(a1, a2, a3);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[HyperFirewall] Exception caught while processing opcode: {e.Message}");
            e.Log();
            return;
        }

        return;
    }
    #endregion

    #region Bewitch
    public unsafe delegate nint NoBewitchActionDelegate(CSGameObject* gameObj, float x, float y, float z, int a5, nint a6);
    [EzHook("40 53 48 83 EC 50 45 33 C0", false)]
    public readonly EzHook<NoBewitchActionDelegate>? BewitchHook;

    private unsafe nint BewitchDetour(CSGameObject* gameObj, float x, float y, float z, int a5, nint a6)
    {
        try
        {
            if (gameObj->IsCharacter())
            {
                var chara = gameObj->Character();
                if (chara->GetStatusManager()->HasStatus(3023) || chara->GetStatusManager()->HasStatus(3024))
                    return nint.Zero;
            }
            return BewitchHook!.Original(gameObj, x, y, z, a5, a6);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex.Message, ex);
            return BewitchHook!.Original(gameObj, x, y, z, a5, a6);
        }
    }
    #endregion

    #region Knockback
    public delegate long kbprocDelegate(long gameobj, float rot, float length, long a4, char a5, int a6);
    [EzHook("E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? FF C6", false)]
    public readonly EzHook<kbprocDelegate>? KBProcHook;

    private long KBProcDetour(long gameobj, float rot, float length, long a4, char a5, int a6) => KBProcHook!.Original(gameobj, rot, 0f, a4, a5, a6);
    #endregion

    #region Achievements
    public delegate void ReceiveAchievementProgressDelegate(Achievement* achievement, uint id, uint current, uint max);
    [EzHook("C7 81 ?? ?? ?? ?? ?? ?? ?? ?? 89 91 ?? ?? ?? ?? 44 89 81")]
    public EzHook<ReceiveAchievementProgressDelegate> ReceiveAchievementProgressHook = null!;

    public void ReceiveAchievementProgressDetour(Achievement* achievement, uint id, uint current, uint max)
    {
        try
        {
            Svc.Log.Debug($"{nameof(ReceiveAchievementProgressDetour)}: [{id}] {current} / {max}");
            Events.OnAchievementProgressUpdate(id, current, max);
        }
        catch (Exception e)
        {
            Svc.Log.Error("Error receiving achievement progress: {e}", e);
        }

        ReceiveAchievementProgressHook.Original(achievement, id, current, max);
    }
    #endregion
}
