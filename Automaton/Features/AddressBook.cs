using Automaton.IPC;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace Automaton.Features;

public class AddresBookConfiguration
{
    public List<(string Name, uint Territory, Vector3 Position)> Locations = [];
}

[Tweak, Requirement(NavmeshIPC.Name, NavmeshIPC.Repo)]
public class AddressBook : Tweak<AddresBookConfiguration>
{
    public override string Name => "Address Book";
    public override string Description => "Path to your favourite locations.\nUse /goto <name> to path to your location.";

    private string _name = string.Empty;
    public override void DrawConfig()
    {
        if (ImGui.InputText($"##NewLocation", ref _name, 50, ImGuiInputTextFlags.EnterReturnsTrue))
            Config.Locations.Add((_name, Player.Territory, Player.Position));
        ImGuiX.DrawSection($"Locations in {GetRow<TerritoryType>(Player.Territory)!.PlaceName.Value!.NameNoArticle}");

        var locs = Config.Locations.Where(x => x.Territory == Player.Territory);
        foreach (var loc in locs)
        {
            ImGuiEx.TextV(Colors.White, $"{loc.Name}: {loc.Position}");
            ImGui.SameLine();
            ImGuiX.PathfindButton(P.Navmesh, loc.Position);
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(loc.Position.GetHashCode(), FontAwesomeIcon.Trash))
                Config.Locations.Remove(loc);
        }

        ImGuiX.DrawSection("All Locations");
        var territories = Config.Locations.GroupBy(x => x.Territory);
        foreach (var t in territories)
        {
            ImGuiX.DrawSection($"{GetRow<TerritoryType>(t.Key)!.PlaceName.Value!.NameNoArticle}", drawSeparator: false);
            foreach (var loc in t)
            {
                ImGuiEx.TextV(Colors.White, $"{loc.Name}: {loc.Position}");
                ImGui.SameLine();
                ImGuiX.PathfindButton(P.Navmesh, loc.Position);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(loc.Position.GetHashCode(), FontAwesomeIcon.Trash))
                    Config.Locations.Remove(loc);
            }
        }
    }

    [CommandHandler("/goto", "Path to saved location")]
    internal void OnCommand(string command, string arguments)
    {
        var place = Config.Locations.FirstOrDefault(l => l.Name.Contains(arguments));
        if (place != default)
            P.Navmesh.PathfindAndMoveTo(place.Position, false);
    }
}
