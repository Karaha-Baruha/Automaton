using ECommons.Automation;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Automaton.Features;

[Tweak]
internal class AutoQueue : Tweak
{
    public override string Name => "Auto Queue";
    public override string Description => "Auto queue into a pre-checked duty.\nTriggers on zone change.\n If everyone in the party is nearby each other in the overworld (targetable range), it will wait for them to be fully loaded into the overworld first.";

    public override void Enable()
    {
        Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "ContentsFinder", OnSetupContentsFinder);
    }

    public override void Disable()
    {
        Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
        Svc.AddonLifecycle.UnregisterListener(OnSetupContentsFinder);
    }

    private unsafe void OnSetupContentsFinder(AddonEvent type, AddonArgs args) => Callback.Fire((AtkUnitBase*)args.Addon, true, 12, 0);

    private unsafe void OnTerritoryChanged(ushort obj)
    {
        if (Player.InDuty || Player.HasPenalty) return;
        TaskManager.Enqueue(() => !IsOccupied());
        TaskManager.Enqueue(() => Svc.Party.All(p => p.Territory.GameData!.TerritoryIntendedUse is (byte)TerritoryIntendedUseEnum.City_Area or (byte)TerritoryIntendedUseEnum.Open_World));
        TaskManager.Enqueue(() => !Svc.Party.All(p => p.Territory.Id == Player.Territory) || Svc.Party.All(p => p.GameObject?.IsTargetable ?? false));
        TaskManager.Enqueue(() => Framework.Instance()->GetUIModule()->ExecuteMainCommand(33));
    }
}
