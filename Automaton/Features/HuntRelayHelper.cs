using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons.Automation;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using static Dalamud.Game.Text.XivChatType;

namespace Automaton.Features;
public class HuntRelayHelperConfiguration
{
    public List<(XivChatType Channel, string Command, bool IsLocal, bool Enabled)> Channels =
    [
        (Ls1, "l1", true, false),
        (Ls2, "l2", true, false),
        (Ls3, "l3", true, false),
        (Ls4, "l4", true, false),
        (Ls5, "l5", true, false),
        (Ls6, "l6", true, false),
        (Ls7, "l7", true, false),
        (Ls8, "l8", true, false),
        (FreeCompany, "fc", true, false),
        (NoviceNetwork, "n", true, false),
        (CrossLinkShell1, "cwl1", false, false),
        (CrossLinkShell2, "cwl2", false, false),
        (CrossLinkShell3, "cwl3", false, false),
        (CrossLinkShell4, "cwl4", false, false),
        (CrossLinkShell5, "cwl5", false, false),
        (CrossLinkShell6, "cwl6", false, false),
        (CrossLinkShell7, "cwl7", false, false),
        (CrossLinkShell8, "cwl8", false, false),
    ];

    [BoolConfig] public bool OnlySendLocalHuntsToLocalChannels = true;
    [BoolConfig] public bool AssumeBlankWorldsAreLocal = false;
    [BoolConfig] public bool DontRepeatRelays = true;
    [BoolConfig] public bool AllowPartialWorldMatches = false;
    [BoolConfig] public bool DryRun = false;
    [StringConfig] public string ChatMessagePattern = "[<world>] <type> -> <flag>";
    [EnumConfig] public HuntRelayHelper.Locality AssumedLocality = HuntRelayHelper.Locality.PlayerHomeWorld;

    public List<(HuntRelayHelper.RelayTypes RelayType, string TypeFormat, string TypeHeuristics)> Types =
    [
        (HuntRelayHelper.RelayTypes.SRank, "S Rank", @"s rank, rank s, /(?:^|\W)[sS](?:$|\W)/"),
        (HuntRelayHelper.RelayTypes.Minions, "Minions", @"ssminion, /\bminions?\b/"),
        (HuntRelayHelper.RelayTypes.Train, "Train", @"train"),
        (HuntRelayHelper.RelayTypes.FATE, "FATE", @"boss, fate"),
    ];
}

[Tweak]
public class HuntRelayHelper : Tweak<HuntRelayHelperConfiguration>
{
    public override string Name => "Hunt Relay Helper";
    public override string Description => "Appends a clickable icon to messages with a MapLinkPayload to relay them to other channels.";

    private DalamudLinkPayload RelayLinkPayload = null!;
    private readonly string InstanceHeuristics = @"\b(?:instance\s*(?<instanceNumber>\d+)|i(?<iNumber>\d+))\b";

    public override void Enable()
    {
        Svc.Chat.CheckMessageHandled += OnChatMessage;
        RelayLinkPayload = Svc.PluginInterface.AddChatLinkHandler(0, HandleRelayLink);
    }

    public override void Disable()
    {
        Svc.Chat.CheckMessageHandled -= OnChatMessage;
        Svc.PluginInterface.RemoveChatLinkHandler(0);
    }

    public enum Locality
    {
        PlayerHomeWorld,
        PlayerCurrentWorld,
        SenderHomeWorld,
    }

    public enum RelayTypes
    {
        SRank,
        Minions,
        Train,
        FATE,

        None, // Keep this last
    }

    public override void DrawConfig()
    {
        ImGuiX.DrawSection("Chat Channels");
        using (ImRaii.Table($"##{nameof(Config.Channels)}", 4))
        {
            foreach (var c in Config.Channels.ToList().Select((x, i) => new { Value = x, Index = i }))
            {
                var column = c.Index % 2 == 0 ? 0 : 2;
                if (column == 0)
                    ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(column);
                ImGui.TextUnformatted(Config.Channels[c.Index].Channel.ToString());

                ImGui.TableSetColumnIndex(column + 1);
                var tmpE = c.Value;
                if (ImGui.Checkbox($"##{c.Value.Channel}{nameof(c.Value.Enabled)}", ref tmpE.Enabled))
                    Config.Channels[c.Index] = (Config.Channels[c.Index].Channel, Config.Channels[c.Index].Command, Config.Channels[c.Index].IsLocal, tmpE.Enabled);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Enable sending to this channel");

                ImGui.SameLine();
                var tmpL = c.Value;
                if (ImGui.Checkbox($"##{c.Value.Channel}{nameof(c.Value.IsLocal)}", ref tmpL.IsLocal))
                    Config.Channels[c.Index] = (Config.Channels[c.Index].Channel, Config.Channels[c.Index].Command, tmpL.IsLocal, Config.Channels[c.Index].Enabled);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Mark channel as \"local\"");
            }
        }

        ImGuiX.DrawSection("Configuration");

        ImGui.Checkbox("Don't repeat relays", ref Config.DontRepeatRelays);
        ImGuiComponents.HelpMarker("Don't send relays to the channel in which you clicked the relay payload.");

        ImGui.Checkbox("Allow partial world matching", ref Config.AllowPartialWorldMatches);
        ImGuiComponents.HelpMarker("This will allow matching shorthands of worlds (e.g. \"behe\" -> Behemoth) but may result in false positives.");

        ImGui.Checkbox("Only send local hunts to local channels", ref Config.OnlySendLocalHuntsToLocalChannels);
        ImGuiComponents.HelpMarker("If a hunt is detected as being off your home world, it will only be relayed to non-local channels.");

        ImGui.Checkbox("Assume blank worlds are local", ref Config.AssumeBlankWorldsAreLocal);
        ImGuiComponents.HelpMarker("If the world is failed to be detected, assume it's meant for the local world.");

        ImGui.Indent();
        foreach (var l in Enum.GetValues<Locality>().Select((x, i) => new { Value = x, Index = i }))
        {
            if (ImGui.RadioButton($"{l.Value.ToString().SplitWords()}", Config.AssumedLocality == l.Value))
                Config.AssumedLocality = l.Value;
            if (l.Index < Enum.GetValues<Locality>().Length - 1)
                ImGui.SameLine();
        }
        ImGui.Unindent();

        ImGui.Checkbox("Dry run", ref Config.DryRun);
        ImGuiComponents.HelpMarker("Enabling this will print the messages to chat without actually sending them to the server. This is just for testing.");

        ImGuiX.DrawSection("Chat Message Pattern");
        ImGui.InputText($"##{nameof(Config.ChatMessagePattern)}", ref Config.ChatMessagePattern, 64);
        ImGuiComponents.HelpMarker("Available tags: <world>, <type>, <flag>");

        ImGuiX.DrawSection("Relay Type Configuration");
        foreach (var t in Config.Types.ToList().Select((x, i) => new { Value = x, Index = i }))
        {
            ImGui.TextUnformatted($"{t.Value.RelayType.ToString().SplitWords()}");
            ImGui.Indent();
            ImGuiEx.TextV("Format: ");
            ImGui.SameLine();
            var tmpF = Config.Types[t.Index].TypeFormat;
            if (ImGui.InputText($"##{t.Value.RelayType}{nameof(t.Value.TypeFormat)}", ref tmpF, 64))
                Config.Types[t.Index] = (t.Value.RelayType, tmpF, t.Value.TypeHeuristics);
            ImGuiComponents.HelpMarker("This is what will be sent in chat to replace the <type> tag.");

            ImGuiEx.TextV("Heuristics: ");
            ImGui.SameLine();
            var tmpH = Config.Types[t.Index].TypeHeuristics;
            if (ImGui.InputText($"##{t.Value.RelayType}{nameof(t.Value.TypeHeuristics)}", ref tmpH, 128))
                Config.Types[t.Index] = (t.Value.RelayType, t.Value.TypeFormat, tmpH);
            ImGuiComponents.HelpMarker("These are the comma separated heuristics to check against in the message to match to a type.\nThis supports regex if you surrouned the heuristic with \"/\"\nAll special text icons are converted to normal text automatically.");
            ImGui.Unindent();
        }
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (sender.TextValue == Player.Name) return;
        var maplink = message.Payloads.FirstOrDefault(x => x is MapLinkPayload, null);
        if (maplink is null) return;

        try
        {
            if (maplink is MapLinkPayload mlp)
            {
                var (world, instance, relayType) = DetectWorldInstanceRelayType(message);
                if ((RelayTypes)relayType == RelayTypes.None)
                {
                    Svc.Log.Info($"Failed to detect relay type in {nameof(MapLinkPayload)} message: {message}");
                    return;
                }
                if (world == null && Config.AssumeBlankWorldsAreLocal)
                {
                    switch (Config.AssumedLocality)
                    {
                        case Locality.PlayerHomeWorld:
                            world = Player.Object.HomeWorld.GameData;
                            break;
                        case Locality.PlayerCurrentWorld:
                            world = Player.Object.CurrentWorld.GameData;
                            break;
                        case Locality.SenderHomeWorld:
                            world = sender.Payloads.OfType<TextPayload>()
                                .Select(p => p.Text!.Contains((char)SeIconChar.CrossWorld) ? FindRow<World>(x => x!.IsPublic && p.Text.Split((char)SeIconChar.CrossWorld)[1].Contains(x.Name.RawString, StringComparison.OrdinalIgnoreCase)) : Player.Object.CurrentWorld.GameData)
                                .FirstOrDefault(Player.Object.CurrentWorld.GameData);
                            break;
                    }
                }
                if (world != null)
                {
                    Svc.Log.Verbose($"Detected world {world.Name} and instance {instance} in {nameof(MapLinkPayload)} message: {message}");
                    message.Payloads.AddRange([RelayLinkPayload, new IconPayload(BitmapFontIcon.NotoriousMonster), new RelayPayload(mlp, world.RowId, instance, relayType, (uint)type).ToRawPayload(), RawPayload.LinkTerminator]);
                }
                else
                    Svc.Log.Info($"Failed to detect world in {nameof(MapLinkPayload)} message: {message}");
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex.Message, ex);
        }
    }

    private void HandleRelayLink(uint _, SeString link)
    {
        var payload = link.Payloads.OfType<RawPayload>().Select(RelayPayload.Parse).FirstOrDefault(x => x != default);
        if (payload == default) { Svc.Log.Error($"Failed to parse {nameof(RelayPayload)}"); return; }
        var relay = BuildRelayMessage(payload.MapLink, payload.World, payload.Instance, payload.RelayType);
        foreach (var (channel, command, islocal, enabled) in Config.Channels)
        {
            if (!enabled) continue;
            // TODO: add a check to see if the player is in novice network before sending
            // TODO: add a crystalline conflict check since non-premade messages can't be sent to any channel then
            if ((XivChatType)payload.OriginChannel == channel && Config.DontRepeatRelays) continue;
            if (channel.GetAttribute<XivChatTypeInfoAttribute>()!.FancyName.StartsWith("Linkshell") && Player.CurrentWorld != Player.HomeWorld) continue;
            if (islocal && Player.Object.CurrentWorld.GameData != payload.World && Config.OnlySendLocalHuntsToLocalChannels) continue;

            TaskManager.EnqueueDelay(500);
#pragma warning disable CS0618 // Type or member is obsolete
            if (Config.DryRun)
                TaskManager.Enqueue(() => Svc.Chat.Print(new XivChatEntry() { Type = (XivChatType)payload.OriginChannel, Message = $"[DRY RUN] {relay.BuiltString}" }));
            else
                TaskManager.Enqueue(() => Chat.Instance.SendMessageUnsafe(Encoding.UTF8.GetBytes($"/{command} {relay.BuiltString}")));
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    private SeStringBuilder BuildRelayMessage(MapLinkPayload MapLink, World World, uint? Instance, uint RelayType)
    {
        var pattern = "(?i)(<flag>|<world>|<type>)";
        var splitMsg = Regex.Split(Config.ChatMessagePattern, pattern);
        var sb = new SeStringBuilder();
        foreach (var s in splitMsg)
        {
            switch (s)
            {
                case "<flag>":
                    if (Instance != default)
                        sb.Append(SeString.CreateMapLinkWithInstance(MapLink.TerritoryType.RowId, MapLink.Map.RowId, (int?)Instance, MapLink.RawX, MapLink.RawY));
                    else
                        sb.Append(SeString.CreateMapLink(MapLink.TerritoryType.RowId, MapLink.Map.RowId, MapLink.RawX, MapLink.RawY));
                    break;
                case "<world>":
                    sb.AddText(World.Name);
                    break;
                case "<type>":
                    switch (RelayType)
                    {
                        case (uint)RelayTypes.SRank:
                            sb.AddText(Config.Types[(int)RelayTypes.SRank].TypeFormat);
                            break;
                        case (uint)RelayTypes.Minions:
                            sb.AddText(Config.Types[(int)RelayTypes.Minions].TypeFormat);
                            break;
                        case (uint)RelayTypes.Train:
                            sb.AddText(Config.Types[(int)RelayTypes.Train].TypeFormat);
                            break;
                        case (uint)RelayTypes.FATE:
                            sb.AddText(Config.Types[(int)RelayTypes.FATE].TypeFormat);
                            break;
                    }
                    break;
                default:
                    sb.AddText(s);
                    break;
            }
        }
        return sb;
    }

    private (World?, uint, uint) DetectWorldInstanceRelayType(SeString message)
    {
        var text = string.Join(" ", message.Payloads.OfType<TextPayload>().Select(x => x.Text));
        var heuristicInstance = 0;
        var mapInstance = text.Select(ReplaceSeIconIntanceNumber).OfType<int>().FirstOrDefault(0);

        // trim texts within MapLinkPayload
        const string linkPattern = ".*?\\)";
        var rgx = new Regex(linkPattern);
        text = rgx.Replace(text, "");
        // replace Boxed letters with alphabets
        text = string.Join(string.Empty, text.Select(ReplaceSeIconChar));

        var instanceMatch = Regex.Match(text, InstanceHeuristics, RegexOptions.IgnoreCase);
        if (instanceMatch.Success)
        {
            if (instanceMatch.Groups["instance"].Success && int.TryParse(instanceMatch.Groups["instance"].Value, out var i1))
                heuristicInstance = i1;
            if (instanceMatch.Groups["iNumber"].Success && int.TryParse(instanceMatch.Groups["iNumber"].Value, out var i2))
                heuristicInstance = i2;
        }

        var relayType = RelayTypes.None;
        foreach (var t in Config.Types)
        {
            if (t.TypeHeuristics.Split(',').Select(x => x.Trim()).Any(x => { return x.StartsWith('/') && x.EndsWith('/') ? Regex.IsMatch(text, x[1..^1], RegexOptions.IgnoreCase) : text.Contains(x, StringComparison.OrdinalIgnoreCase); }))
            {
                relayType = t.RelayType;
                break;
            }
        }

        World? partial = null;
        if (Config.AllowPartialWorldMatches)
            foreach (var word in RemoveConflicts(text).Split(' ').Where(t => !ECommons.GenericHelpers.IsNullOrEmpty(t) && t.Length > 2))
                partial ??= FindRow<World>(x => x!.IsPublic && x.DataCenter.Value!.Name == Player.CurrentDataCenter && x.Name.RawString.Contains(RemoveNonAlphaNumeric(word), StringComparison.OrdinalIgnoreCase));

        return (partial ?? FindRow<World>(x => x!.IsPublic && RemoveConflicts(text).Contains(x.Name.RawString, StringComparison.OrdinalIgnoreCase)) ?? null, heuristicInstance != 0 ? (uint)heuristicInstance : (uint)mapInstance, (uint)relayType);
    }

    // I think this is the only case where an S rank has the name of a world contained within it
    private string RemoveConflicts(string text) => text.Replace("kaiser behemoth", string.Empty, StringComparison.OrdinalIgnoreCase);
    private string RemoveNonAlphaNumeric(string text) => Regex.Replace(text, @"\W+", "");

    private char ReplaceSeIconChar(char c)
    {
        return c switch
        {
            (char)SeIconChar.BoxedLetterA => 'A',
            (char)SeIconChar.BoxedLetterB => 'B',
            (char)SeIconChar.BoxedLetterC => 'C',
            (char)SeIconChar.BoxedLetterD => 'D',
            (char)SeIconChar.BoxedLetterE => 'E',
            (char)SeIconChar.BoxedLetterF => 'F',
            (char)SeIconChar.BoxedLetterG => 'G',
            (char)SeIconChar.BoxedLetterH => 'H',
            (char)SeIconChar.BoxedLetterI => 'I',
            (char)SeIconChar.BoxedLetterJ => 'J',
            (char)SeIconChar.BoxedLetterK => 'K',
            (char)SeIconChar.BoxedLetterL => 'L',
            (char)SeIconChar.BoxedLetterM => 'M',
            (char)SeIconChar.BoxedLetterN => 'N',
            (char)SeIconChar.BoxedLetterO => 'O',
            (char)SeIconChar.BoxedLetterP => 'P',
            (char)SeIconChar.BoxedLetterQ => 'Q',
            (char)SeIconChar.BoxedLetterR => 'R',
            (char)SeIconChar.BoxedLetterS => 'S',
            (char)SeIconChar.BoxedLetterT => 'T',
            (char)SeIconChar.BoxedLetterU => 'U',
            (char)SeIconChar.BoxedLetterV => 'V',
            (char)SeIconChar.BoxedLetterW => 'W',
            (char)SeIconChar.BoxedLetterX => 'X',
            (char)SeIconChar.BoxedLetterY => 'Y',
            (char)SeIconChar.BoxedLetterZ => 'Z',
            _ => c,
        };
    }

    private int? ReplaceSeIconIntanceNumber(char c)
    {
        return c switch
        {
            (char)SeIconChar.Instance1 => 1,
            (char)SeIconChar.Instance2 => 2,
            (char)SeIconChar.Instance3 => 3,
            (char)SeIconChar.Instance4 => 4,
            (char)SeIconChar.Instance5 => 5,
            (char)SeIconChar.Instance6 => 6,
            (char)SeIconChar.Instance7 => 7,
            (char)SeIconChar.Instance8 => 8,
            (char)SeIconChar.Instance9 => 9,
            _ => null
        };
    }

    private int ReplaceSeIconCharNumber(char c)
    {
        return c switch
        {
            (char)SeIconChar.Number1 => 1,
            (char)SeIconChar.BoxedNumber1 => 1,
            (char)SeIconChar.Number2 => 2,
            (char)SeIconChar.BoxedNumber2 => 2,
            (char)SeIconChar.Number3 => 3,
            (char)SeIconChar.BoxedNumber3 => 3,
            (char)SeIconChar.Number4 => 4,
            (char)SeIconChar.BoxedNumber4 => 4,
            (char)SeIconChar.Number5 => 5,
            (char)SeIconChar.BoxedNumber5 => 5,
            (char)SeIconChar.Number6 => 6,
            (char)SeIconChar.BoxedNumber6 => 6,
            (char)SeIconChar.Number7 => 7,
            (char)SeIconChar.BoxedNumber7 => 7,
            (char)SeIconChar.Number8 => 8,
            (char)SeIconChar.BoxedNumber8 => 8,
            (char)SeIconChar.Number9 => 9,
            (char)SeIconChar.BoxedNumber9 => 9,
            _ => c,
        };
    }
}
