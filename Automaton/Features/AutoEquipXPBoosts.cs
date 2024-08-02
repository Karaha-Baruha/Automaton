namespace Automaton.Features;

[Tweak]
internal class AutoEquipXPBoosts : Tweak
{
    public override string Name => "Auto Equip DT Earrings";
    public override string Description => "Automatically equip the DT earrings when level synced to <= 90.";

    public override void Enable() => Svc.Framework.Update += CheckForLevelSync;
    public override void Disable() => Svc.Framework.Update -= CheckForLevelSync;

    private unsafe void CheckForLevelSync(IFramework framework)
    {
        if (!Player.IsLevelSynced || Player.SyncedLevel == 0 || IsOccupied()) return;

        if (Player.SyncedLevel <= 90)
            if (Inventory.HasItem(41081))
                Player.Equip(41081);
    }
}
