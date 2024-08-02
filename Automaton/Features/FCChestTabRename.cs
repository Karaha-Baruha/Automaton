using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Automaton.Features;

public class FcChestTabRenameConfiguration
{
    [StringConfig] public string TabOne = string.Empty;
    [StringConfig] public string TabTwo = string.Empty;
    [StringConfig] public string TabThree = string.Empty;
    [StringConfig] public string TabFour = string.Empty;
    [StringConfig] public string TabFive = string.Empty;
}

[Tweak]
internal class FCChestTabRename : Tweak<FcChestTabRenameConfiguration>
{
    public override string Name => "Custom FC Chest Tab Names";
    public override string Description => "Rename the tabs in the Free Company chest to whatever your heart desires.";

    public override void Enable()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "", PreDraw);
    }

    public override void Disable()
    {
        Svc.AddonLifecycle.UnregisterListener(PreDraw);
    }

    private unsafe void PreDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon;
        if (Config.TabOne != string.Empty)
            Utils.GetNodeByIDChain(addon->GetRootNode(), 1, 9, 10, 9)->GetAsAtkTextNode()->NodeText.SetString(Config.TabOne);
        if (Config.TabTwo != string.Empty)
            Utils.GetNodeByIDChain(addon->GetRootNode(), 1, 9, 11, 9)->GetAsAtkTextNode()->NodeText.SetString(Config.TabTwo);
        if (Config.TabThree != string.Empty)
            Utils.GetNodeByIDChain(addon->GetRootNode(), 1, 9, 12, 9)->GetAsAtkTextNode()->NodeText.SetString(Config.TabThree);
        if (Config.TabFour != string.Empty)
            Utils.GetNodeByIDChain(addon->GetRootNode(), 1, 9, 13, 9)->GetAsAtkTextNode()->NodeText.SetString(Config.TabFour);
        if (Config.TabFive != string.Empty)
            Utils.GetNodeByIDChain(addon->GetRootNode(), 1, 9, 14, 9)->GetAsAtkTextNode()->NodeText.SetString(Config.TabFive);
    }
}
