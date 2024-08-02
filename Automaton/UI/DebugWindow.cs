using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace Automaton.UI;

internal class DebugWindow : Window
{
    public DebugWindow() : base($"{Name} - Debug {P.GetType().Assembly.GetName().Version}###{Name}{nameof(DebugWindow)}")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public static void Dispose()
    {

    }

    private int id;
    public unsafe override void Draw()
    {
        var agent = AgentMap.Instance();
        if (agent == null) return;
        var markers = agent->MiniMapGatheringMarkers;
        ImGuiX.DrawSection($"markers: {markers.Length}");
        foreach (var marker in markers)
        {
            if (marker.MapMarker.IconId == 0) continue;
            ImGui.TextUnformatted($"{marker.MapMarker.X}, {marker.MapMarker.Y}, {marker.MapMarker.IconId} {marker.MapMarker.IconFlags} {marker.MapMarker.SecondaryIconId}");
            ImGui.Indent();
            ImGui.TextUnformatted($"{MapUtil.WorldToMap(new Vector2(marker.MapMarker.X / 16, marker.MapMarker.Y / 16))}");
            ImGui.Unindent();
        }
    }
}
