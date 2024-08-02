using Automaton.Utilities.Movement;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;

namespace Automaton.Features;

public class AutoFollowConfiguration
{
    [EnumConfig] public Utilities.Utils.MovementType MovementType;

    [IntConfig(DefaultValue = 3)] public int DistanceToKeep = 3;
    [IntConfig] public int DisableIfFurtherThan;
    [BoolConfig] public bool OnlyInDuty;
    [BoolConfig] public bool ChangeMasterOnChat;
    [BoolConfig] public bool MountAndFly;
    [StringConfig] public string AutoFollowName = string.Empty;
}

[Tweak]
public unsafe class AutoFollow : Tweak<AutoFollowConfiguration>
{
    public override string Name => "Auto Follow";
    public override string Description
        => "True Auto Follow. Trigger with /autofollow while targeting someone. Use it with no target to wipe the current master.\n" +
        "If multiboxing, you can send \"autofollow\" to chat and anyone in the party with this feature enabled will follow.\n" +
        "You can also add a number argument to specify the distance to keep, or add the off argument to clear the current master.";

    private readonly OverrideMovement movement = new();
    private DGameObject? master;
    private uint? masterObjectID;

    [CommandHandler("/autofollow", "Enable AutoFollow")]
    internal void OnCommand(string command, string arguments)
    {
        if (!arguments.IsNullOrEmpty())
        {
            var obj = Svc.Objects.FirstOrDefault(o => o.Name.TextValue.ToLowerInvariant().Contains(arguments, StringComparison.InvariantCultureIgnoreCase));
            if (obj != null)
            {
                master = obj;
                masterObjectID = obj.EntityId;
                return;
            }
        }
        if (Svc.Targets.Target != null)
            SetMaster();
        else
            ClearMaster();
    }

    public override void Enable()
    {
        Svc.Framework.Update += Follow;
        Svc.Chat.ChatMessage += OnChatMessage;
    }

    public override void Disable()
    {
        Svc.Framework.Update -= Follow;
        Svc.Chat.ChatMessage -= OnChatMessage;
    }

    private void SetMaster()
    {
        try
        {
            master = Svc.Targets.Target;
            masterObjectID = Svc.Targets.Target?.EntityId;
        }
        catch { return; }
    }

    private void ClearMaster()
    {
        master = null;
        masterObjectID = null;
    }

    private void Follow(IFramework framework)
    {
        if (!Player.Available) return;

        master = Svc.Objects.FirstOrDefault(x => x.EntityId == masterObjectID || !Config.AutoFollowName.IsNullOrEmpty() && x.Name.TextValue.Equals(Config.AutoFollowName, StringComparison.InvariantCultureIgnoreCase));

        if (master == null) { movement.Enabled = false; return; }
        if (Config.DisableIfFurtherThan > 0 && !Player.Object.IsNear(master, Config.DisableIfFurtherThan)) { movement.Enabled = false; return; }
        if (Config.OnlyInDuty && !Player.InDuty) { movement.Enabled = false; return; }
        if (Svc.Condition[ConditionFlag.InFlight]) { TaskManager.Abort(); }

        if (master.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
        {
            if (master.Character()->IsMounted() && CanMount())
            {
                movement.Enabled = false;
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }

            if (Config.MountAndFly && ((Structs.Character*)master.Address)->IsFlying != 0 && !Svc.Condition[ConditionFlag.InFlight] && Svc.Condition[ConditionFlag.Mounted])
            {
                movement.Enabled = false;
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                TaskManager.EnqueueDelay(50);
                TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2));
                return;
            }

            if (!(master.Character()->IsMounted() && Svc.Condition[ConditionFlag.Mounted]) && TerritorySupportsMounting())
            {
                movement.Enabled = false;
                master.BattleChara()->GetStatusManager()->RemoveStatus(10);
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 9);
                return;
            }
        }

        // set dismount logic here

        if (Vector3.Distance(Player.Position, master.Position) <= Config.DistanceToKeep) { movement.Enabled = false; return; }

        movement.Enabled = true;
        movement.DesiredPosition = master.Position;
    }

    private static bool CanMount() => !Svc.Condition[ConditionFlag.Mounted] && !Svc.Condition[ConditionFlag.Mounting] && !Svc.Condition[ConditionFlag.InCombat] && !Svc.Condition[ConditionFlag.Casting];

    private static bool TerritorySupportsMounting() => GetRow<TerritoryType>(Player.Territory)?.Unknown32 != 0;

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type != XivChatType.Party) return;
        var player = sender.Payloads.SingleOrDefault(x => x is PlayerPayload) as PlayerPayload;
        if (message.TextValue.ToLowerInvariant().Contains("autofollow"))
        {
            if (int.TryParse(message.TextValue.Split("autofollow")[1], out var distance))
                Config.DistanceToKeep = distance;
            else if (message.TextValue.ToLowerInvariant().Contains("autofollow off"))
                ClearMaster();
            else
            {
                foreach (var actor in Svc.Objects)
                {
                    if (actor == null) continue;
                    if (actor.Name.TextValue.Equals(player?.PlayerName))
                    {
                        Svc.Targets.Target = actor;
                        SetMaster();
                    }
                }
            }
        }
    }
}
