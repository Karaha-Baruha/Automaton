using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.Reflection;
using ECommons.SimpleGui;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using System.Reflection;

namespace Automaton.Utilities;
public static class Utils
{
    public enum MovementType
    {
        Direct,
        Pathfind
    }

    public static IDalamudTextureWrap? GetIcon(uint iconId) => iconId != 0 ? Svc.Texture?.GetFromGameIcon(iconId).GetWrapOrEmpty() : null;

    public static bool HasPlugin(string name) => DalamudReflector.TryGetDalamudPlugin(name, out _, false, true);

    private static readonly Dictionary<Type, AgentId> AgentIdCache = [];
    public static unsafe T* GetAgent<T>(AgentId id) where T : unmanaged
        => (T*)AgentModule.Instance()->GetAgentByInternalId(id);

    public static unsafe T* GetAgent<T>() where T : unmanaged
    {
        var type = typeof(T);

        if (!AgentIdCache.TryGetValue(type, out var id))
        {
            var attr = type.GetCustomAttribute<AgentAttribute>(false)
                ?? throw new Exception($"Agent {type.FullName} is missing AgentAttribute");

            AgentIdCache.Add(type, id = attr.Id);
        }

        return GetAgent<T>(id);
    }

    public const int UnitListCount = 18;
    public static unsafe AtkUnitBase* GetAddonByID(uint id)
    {
        var unitManagers = &AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.DepthLayerOneList;
        for (var i = 0; i < UnitListCount; i++)
        {
            var unitManager = &unitManagers[i];
            foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->Entries.Length)))
            {
                var unitBase = unitManager->Entries[j].Value;
                if (unitBase != null && unitBase->Id == id)
                {
                    return unitBase;
                }
            }
        }

        return null;
    }

    public static unsafe bool IsClickingInGameWorld()
        => !ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow)
        && !ImGui.GetIO().WantCaptureMouse
        && AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList.Count == 0
        && Framework.Instance()->Cursor->ActiveCursorType == 0;

    public static Vector3 RotatePoint(float cx, float cy, float angle, Vector3 p)
    {
        if (angle == 0f) return p;
        var s = (float)Math.Sin(angle);
        var c = (float)Math.Cos(angle);

        // translate point back to origin:
        p.X -= cx;
        p.Z -= cy;

        // rotate point
        var xnew = p.X * c - p.Z * s;
        var ynew = p.X * s + p.Z * c;

        // translate point back:
        p.X = xnew + cx;
        p.Z = ynew + cy;
        return p;
    }

    public static unsafe Structs.AgentMJICraftSchedule* Agent = (Structs.AgentMJICraftSchedule*)AgentModule.Instance()->GetAgentByInternalId(AgentId.MJICraftSchedule);
    public static unsafe Structs.AgentMJICraftSchedule.AgentData* AgentData => Agent != null ? Agent->Data : null;

    public static unsafe void SetRestCycles(uint mask)
    {
        Svc.Log.Debug($"Setting rest: {mask:X}");
        AgentData->NewRestCycles = mask;
        SynthesizeEvent(5, [new() { Type = AtkValueType.Int, Int = 0 }]);
    }

    private static unsafe void SynthesizeEvent(ulong eventKind, Span<AtkValue> args)
    {
        var eventData = stackalloc int[] { 0, 0, 0 };
        Agent->AgentInterface.ReceiveEvent((AtkValue*)eventData, args.GetPointer(0), (uint)args.Length, eventKind);
    }

    public static T GetService<T>()
    {
        Svc.Log.Info($"Requesting {typeof(T)}");
        var service = typeof(IDalamudPlugin).Assembly.GetType("Dalamud.Service`1")!.MakeGenericType(typeof(T));
        var get = service.GetMethod("Get", BindingFlags.Public | BindingFlags.Static)!;
        return (T)get.Invoke(null, null)!;
    }

    public static bool AllNull(params object[] objects) => objects.All(s => s == null);
    public static bool AnyNull(params object[] objects) => objects.Any(s => s == null);

    public static T? GetWindow<T>() where T : Window
    {
        if (!typeof(T).IsSubclassOf(typeof(Window)))
            return null;
        return EzConfigGui.WindowSystem.Windows.FirstOrDefault(w => w.GetType() == typeof(T)) as T;
    }

    public static void RemoveWindow<T>() where T : Window
    {
        if (!typeof(T).IsSubclassOf(typeof(Window)))
            return;
        var window = EzConfigGui.WindowSystem.Windows.FirstOrDefault(w => w.GetType() == typeof(T));
        if (window != null)
            EzConfigGui.WindowSystem.RemoveWindow(window);
    }

    public static unsafe AtkResNode* GetNodeByIDChain(AtkResNode* node, params int[] ids)
    {
        if (node == null || ids.Length <= 0)
            return null;

        if (node->NodeId == ids[0])
        {
            if (ids.Length == 1)
                return node;

            var newList = new List<int>(ids);
            newList.RemoveAt(0);

            var childNode = node->ChildNode;
            if (childNode != null)
                return GetNodeByIDChain(childNode, [.. newList]);

            if ((int)node->Type >= 1000)
            {
                var componentNode = node->GetAsAtkComponentNode();
                var component = componentNode->Component;
                var uldManager = component->UldManager;
                childNode = uldManager.NodeList[0];
                return childNode == null ? null : GetNodeByIDChain(childNode, [.. newList]);
            }

            return null;
        }

        //check siblings
        var sibNode = node->PrevSiblingNode;
        return sibNode != null ? GetNodeByIDChain(sibNode, ids) : null;
    }

    public static unsafe bool GetUnitBase(string name, out AtkUnitBase* unitBase, int index = 1)
    {
        unitBase = GetUnitBase(name, index);
        return unitBase != null;
    }

    public static unsafe AtkUnitBase* GetUnitBase(string name, int index = 1) => (AtkUnitBase*)Svc.GameGui.GetAddonByName(name, index);

    public static unsafe T* GetUnitBase<T>(string? name = null, int index = 1) where T : unmanaged
    {
        if (string.IsNullOrEmpty(name))
        {
            var attr = (Addon)typeof(T).GetCustomAttribute(typeof(Addon));
            if (attr != null)
            {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        return string.IsNullOrEmpty(name) ? null : (T*)Svc.GameGui.GetAddonByName(name, index);
    }

    public static unsafe bool GetUnitBase<T>(out T* unitBase, string? name = null, int index = 1) where T : unmanaged
    {
        unitBase = null;
        if (string.IsNullOrEmpty(name))
        {
            var attr = (Addon)typeof(T).GetCustomAttribute(typeof(Addon));
            if (attr != null)
            {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) return false;

        unitBase = (T*)Svc.GameGui.GetAddonByName(name, index);
        return unitBase != null;
    }
}
