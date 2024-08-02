using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets;

namespace Automaton.Utilities;

public static class Coords
{
    public static uint GetNearestAetheryte(MapMarkerData marker) => GetNearestAetheryte(marker.TerritoryTypeId, new Vector3(marker.X, marker.Y, marker.Z));
    public static uint GetNearestAetheryte(FlagMapMarker flag) => GetNearestAetheryte((int)flag.TerritoryId, new Vector3(flag.XFloat, 0, flag.YFloat));

    public static uint GetNearestAetheryte(int zoneID, Vector3 pos)
    {
        uint aetheryte = 0;
        double distance = 0;
        foreach (var data in GetSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            if (data.Territory.Value.RowId == zoneID)
            {
                var mapMarker = FindRow<MapMarker>(m => m?.DataType == 3 && m.DataKey == data.RowId);
                if (mapMarker == null)
                {
                    Svc.Log.Error($"Cannot find aetherytes position for {zoneID}#{data.PlaceName.Value.Name}");
                    continue;
                }
                var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, 100);
                var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, 100);
                var temp_distance = Math.Pow(AethersX - pos.X, 2) + Math.Pow(AethersY - pos.Z, 2);
                if (aetheryte == default || temp_distance < distance)
                {
                    distance = temp_distance;
                    aetheryte = data.RowId;
                }
            }
        }

        return aetheryte;
    }

    public static uint? GetPrimaryAetheryte(uint zoneID) => FindRow<Aetheryte>(a => a?.Territory.Value != null && a.Territory.Value.RowId == zoneID)?.RowId ?? null;

    private static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return ConvertRawPositionToMapCoordinate(rawPosition, scale);
    }

    private static float ConvertRawPositionToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
    }

    public static unsafe void TeleportToAetheryte(uint aetheryteID)
    {
        Telepo.Instance()->Teleport(aetheryteID, 0);
    }

    private static TextPayload? GetInstanceIcon(int? instance)
    {
        return instance switch
        {
            1 => new TextPayload(SeIconChar.Instance1.ToIconString()),
            2 => new TextPayload(SeIconChar.Instance2.ToIconString()),
            3 => new TextPayload(SeIconChar.Instance3.ToIconString()),
            _ => default,
        };
    }

    public static uint? GetMapID(uint territory) => GetRow<TerritoryType>(territory)?.Map.Value?.RowId ?? null;
    public static float GetMapScale(uint? territory = null) => GetRow<TerritoryType>(territory ?? Svc.ClientState.TerritoryType)?.Map.Value?.SizeFactor ?? 100f;
}
