using ClickLib.Clicks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;
using Dalamud.Memory;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Newtonsoft.Json;
using Automaton.FeaturesSetup;
using Automaton.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.Components;

namespace Automaton.Features
{
    public abstract class BaseFeature
    {
        protected Automaton P;
        protected DalamudPluginInterface Pi;
        protected Configuration config;
        protected TaskManager TaskManager;
        public FeatureProvider Provider { get; private set; } = null!;

        public virtual bool Enabled { get; protected set; }

        public abstract string Name { get; }

        public virtual string Key => GetType().Name;

        public abstract string Description { get; }

        private uint? jobID = Svc.ClientState.LocalPlayer?.ClassJob.Id;
        public uint? JobID
        {
            get => jobID;
            set
            {
                if (value != null && jobID != value)
                {
                    jobID = value;
                    OnJobChanged?.Invoke(value);
                }
            }
        }

        public delegate void OnJobChangeDelegate(uint? jobId);
        public event OnJobChangeDelegate OnJobChanged;

        public static readonly SeString PandoraPayload = new SeString(new UIForegroundPayload(32)).Append($"{SeIconChar.BoxedLetterP.ToIconString()}{SeIconChar.BoxedLetterA.ToIconString()}{SeIconChar.BoxedLetterN.ToIconString()}{SeIconChar.BoxedLetterD.ToIconString()}{SeIconChar.BoxedLetterO.ToIconString()}{SeIconChar.BoxedLetterR.ToIconString()}{SeIconChar.BoxedLetterA.ToIconString()} ").Append(new UIForegroundPayload(0));
        public virtual void Draw() { }

        public virtual bool Ready { get; protected set; }

        public virtual FeatureType FeatureType { get; }

        public virtual bool isDebug { get; }

        public void InterfaceSetup(Automaton plugin, DalamudPluginInterface pluginInterface, Configuration config, FeatureProvider fp)
        {
            this.P = plugin;
            this.Pi = pluginInterface;
            this.config = config;
            this.Provider = fp;
            this.TaskManager = new();
        }

        public virtual void Setup()
        {
            TaskManager.TimeoutSilently = true;
            Ready = true;
        }

        public virtual void Enable()
        {
            PluginLog.Debug($"Enabling {Name}");
            Svc.Framework.Update += CheckJob;
            Enabled = true;
        }

        private void CheckJob(IFramework framework)
        {
            if (Svc.ClientState.LocalPlayer is null) return;
            JobID = Svc.ClientState.LocalPlayer.ClassJob.Id;
        }

        public virtual void Disable()
        {
            Svc.Framework.Update -= CheckJob;
            Enabled = false;
        }

        public virtual void Dispose()
        {
            Ready = false;
        }

        protected T LoadConfig<T>() where T : FeatureConfig => LoadConfig<T>(this.Key);

        protected T LoadConfig<T>(string key) where T : FeatureConfig
        {
            try
            {
                var configDirectory = pi.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, key + ".json");
                if (!File.Exists(configFile)) return default;
                var jsonString = File.ReadAllText(configFile);
                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to load config for feature {Name}");
                return default;
            }
        }

        protected void SaveConfig<T>(T config) where T : FeatureConfig => SaveConfig<T>(config, this.Key);

        protected void SaveConfig<T>(T config, string key) where T : FeatureConfig
        {
            try
            {
                var configDirectory = pi.GetPluginConfigDirectory();
                var configFile = Path.Combine(configDirectory, key + ".json");
                var jsonString = JsonConvert.SerializeObject(config, Formatting.Indented);

                File.WriteAllText(configFile, jsonString);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Feature failed to write config {this.Name}");
            }
        }

        private void DrawAutoConfig()
        {
            var configChanged = false;
            try
            {
                // ReSharper disable once PossibleNullReferenceException
                var configObj = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(FeatureConfig))).GetValue(this);


                var fields = configObj.GetType().GetFields()
                    .Where(f => f.GetCustomAttribute(typeof(FeatureConfigOptionAttribute)) != null)
                    .Select(f => (f, (FeatureConfigOptionAttribute)f.GetCustomAttribute(typeof(FeatureConfigOptionAttribute))))
                    .OrderBy(a => a.Item2.Priority).ThenBy(a => a.Item2.Name);

                var configOptionIndex = 0;
                foreach (var (f, attr) in fields)
                {
                    if (attr.ConditionalDisplay)
                    {
                        var conditionalMethod = configObj.GetType().GetMethod($"ShouldShow{f.Name}", BindingFlags.Public | BindingFlags.Instance);
                        if (conditionalMethod != null)
                        {
                            var shouldShow = (bool)(conditionalMethod.Invoke(configObj, Array.Empty<object>()) ?? true);
                            if (!shouldShow) continue;
                        }
                    }

                    if (attr.SameLine) ImGui.SameLine();

                    if (attr.Editor != null)
                    {
                        var v = f.GetValue(configObj);
                        var arr = new[] { $"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", v };
                        var o = (bool)attr.Editor.Invoke(null, arr);
                        if (o)
                        {
                            configChanged = true;
                            f.SetValue(configObj, arr[1]);
                        }
                    }
                    else if (f.FieldType == typeof(bool))
                    {
                        var v = (bool)f.GetValue(configObj);
                        if (ImGui.Checkbox($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v))
                        {
                            configChanged = true;
                            f.SetValue(configObj, v);
                        }
                    }
                    else if (f.FieldType == typeof(int))
                    {
                        var v = (int)f.GetValue(configObj);
                        ImGui.SetNextItemWidth(attr.EditorSize == -1 ? -1 : attr.EditorSize * ImGui.GetIO().FontGlobalScale);
                        var e = attr.IntType switch
                        {
                            FeatureConfigOptionAttribute.NumberEditType.Slider => ImGui.SliderInt($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, attr.IntMin, attr.IntMax),
                            FeatureConfigOptionAttribute.NumberEditType.Drag => ImGui.DragInt($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, 1f, attr.IntMin, attr.IntMax),
                            _ => false
                        };

                        if (v % attr.IntIncrements != 0)
                        {
                            v = v.RoundOff(attr.IntIncrements);
                            if (v < attr.IntMin) v = attr.IntMin;
                            if (v > attr.IntMax) v = attr.IntMax;
                        }

                        if (attr.EnforcedLimit && v < attr.IntMin)
                        {
                            v = attr.IntMin;
                            e = true;
                        }

                        if (attr.EnforcedLimit && v > attr.IntMax)
                        {
                            v = attr.IntMax;
                            e = true;
                        }

                        if (e)
                        {
                            f.SetValue(configObj, v);
                            configChanged = true;
                        }
                    }
                    else if (f.FieldType == typeof(float))
                    {
                        var v = (float)f.GetValue(configObj);
                        ImGui.SetNextItemWidth(attr.EditorSize == -1 ? -1 : attr.EditorSize * ImGui.GetIO().FontGlobalScale);
                        var e = attr.IntType switch
                        {
                            FeatureConfigOptionAttribute.NumberEditType.Slider => ImGui.SliderFloat($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, attr.FloatMin, attr.FloatMax, attr.Format),
                            FeatureConfigOptionAttribute.NumberEditType.Drag => ImGui.DragFloat($"{attr.Name}##{f.Name}_{this.GetType().Name}_{configOptionIndex++}", ref v, 1f, attr.FloatMin, attr.FloatMax, attr.Format),
                            _ => false
                        };

                        if (v % attr.FloatIncrements != 0)
                        {
                            v = v.RoundOff(attr.FloatIncrements);
                            if (v < attr.FloatMin) v = attr.FloatMin;
                            if (v > attr.FloatMax) v = attr.FloatMax;
                        }

                        if (attr.EnforcedLimit && v < attr.FloatMin)
                        {
                            v = attr.FloatMin;
                            e = true;
                        }

                        if (attr.EnforcedLimit && v > attr.FloatMax)
                        {
                            v = attr.FloatMax;
                            e = true;
                        }

                        if (e)
                        {
                            f.SetValue(configObj, v);
                            configChanged = true;
                        }
                    }
                    else
                    {
                        ImGui.Text($"Invalid Auto Field Type: {f.Name}");
                    }

                    if (attr.HelpText != null)
                        ImGuiComponents.HelpMarker(attr.HelpText);
                }

                if (configChanged)
                {
                    SaveConfig((FeatureConfig)configObj);
                }

            }
            catch (Exception ex)
            {
                ImGui.Text($"Error with AutoConfig: {ex.Message}");
                ImGui.TextWrapped($"{ex.StackTrace}");
            }
        }

        public virtual bool UseAutoConfig => false;

        public string LocalizedName => this.Name;

        public bool DrawConfig(ref bool hasChanged)
        {
            var configTreeOpen = false;
            if ((UseAutoConfig || DrawConfigTree != null) && Enabled)
            {
                var x = ImGui.GetCursorPosX();
                if (ImGui.TreeNode($"{this.Name}##treeConfig_{GetType().Name}"))
                {
                    configTreeOpen = true;
                    ImGui.SetCursorPosX(x);
                    ImGui.BeginGroup();
                    if (UseAutoConfig)
                        DrawAutoConfig();
                    else
                        DrawConfigTree(ref hasChanged);
                    ImGui.EndGroup();
                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x0);
                ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
                ImGui.TreeNodeEx(LocalizedName, ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen);
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
            }

            if (hasChanged && Enabled) ConfigChanged();
            return configTreeOpen;
        }

        protected delegate void DrawConfigDelegate(ref bool hasChanged);
        protected virtual DrawConfigDelegate DrawConfigTree => null;

        protected virtual void ConfigChanged()
        {
            if (this is null) return;

            var config = this.GetType().GetProperties().FirstOrDefault(p => p.PropertyType.IsSubclassOf(typeof(FeatureConfig)));

            if (config != null)
            {
                var configObj = config.GetValue(this);
                if (configObj != null)
                    SaveConfig((FeatureConfig)configObj);
            }
        }

        public unsafe bool IsRpWalking()
        {
            if (Svc.ClientState.LocalPlayer == null) return false;
            //var atkArrayDataHolder = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            //if (atkArrayDataHolder.NumberArrays[72]->IntArray[6] == 1)
            //    return true;
            //else
            //    return false;

            if (Svc.GameGui.GetAddonByName("_DTR") == IntPtr.Zero) return false;

            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("_DTR");
            if (addon->UldManager.NodeListCount < 9) return false;

            try
            {
                var isVisible = addon->GetNodeById(10)->IsVisible;
                return isVisible;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        internal static unsafe int GetInventoryFreeSlotCount()
        {
            var types = new InventoryType[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 };
            var c = InventoryManager.Instance();
            var slots = 0;
            foreach (var x in types)
            {
                var inv = c->GetInventoryContainer(x);
                for (var i = 0; i < inv->Size; i++)
                {
                    if (inv->Items[i].ItemID == 0)
                    {
                        slots++;
                    }
                }
            }
            return slots;
        }

        internal static unsafe bool IsTargetLocked => *(byte*)(((nint)TargetSystem.Instance()) + 309) == 1;
        internal static bool IsInventoryFree()
        {
            return GetInventoryFreeSlotCount() >= 1;
        }

        public unsafe bool IsMoving() => AgentMap.Instance()->IsPlayerMoving == 1;

        public void PrintModuleMessage(String msg)
        {
            var message = new XivChatEntry
            {
                Message = new SeStringBuilder()
                .AddUiForeground($"[{Automaton.Name}] ", 45)
                .AddUiForeground($"[{Name}] ", 62)
                .AddText(msg)
                .Build()
            };

            Svc.Chat.Print(message);
        }

        public void PrintModuleMessage(SeString msg)
        {
            var message = new XivChatEntry
            {
                Message = new SeStringBuilder()
                .AddUiForeground($"[{Automaton.Name}] ", 45)
                .AddUiForeground($"[{Name}] ", 62)
                .Append(msg)
                .Build()
            };

            Svc.Chat.Print(message);
        }

        internal static unsafe AtkUnitBase* GetSpecificYesno(Predicate<string> compare)
        {
            for (var i = 1; i < 100; i++)
            {
                try
                {
                    var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno", i);
                    if (addon == null) return null;
                    if (GenericHelpers.IsAddonReady(addon))
                    {
                        var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
                        var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
                        if (compare(text))
                        {
                            PluginLog.Verbose($"SelectYesno {text} addon {i} by predicate");
                            return addon;
                        }
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error("", e);
                    return null;
                }
            }
            return null;
        }

        internal static unsafe AtkUnitBase* GetSpecificYesno(params string[] s)
        {
            for (var i = 1; i < 100; i++)
            {
                try
                {
                    var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno", i);
                    if (addon == null) return null;
                    if (GenericHelpers.IsAddonReady(addon))
                    {
                        var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
                        var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText().Replace(" ", "");
                        if (text.EqualsAny(s.Select(x => x.Replace(" ", ""))))
                        {
                            PluginLog.Verbose($"SelectYesno {s.Print()} addon {i}");
                            return addon;
                        }
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error("", e);
                    return null;
                }
            }
            return null;
        }

        internal static bool TrySelectSpecificEntry(string text, Func<bool> Throttler = null)
        {
            return TrySelectSpecificEntry(new string[] { text }, Throttler);
        }

        internal static unsafe bool TrySelectSpecificEntry(IEnumerable<string> text, Func<bool> Throttler = null)
        {
            if (GenericHelpers.TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && GenericHelpers.IsAddonReady(&addon->AtkUnitBase))
            {
                var entry = GetEntries(addon).FirstOrDefault(x => x.ContainsAny(text));
                if (entry != null)
                {
                    var index = GetEntries(addon).IndexOf(entry);
                    if (index >= 0 && IsSelectItemEnabled(addon, index) && (Throttler?.Invoke() ?? GenericThrottle))
                    {
                        ClickSelectString.Using((nint)addon).SelectItem((ushort)index);
                        PluginLog.Debug($"TrySelectSpecificEntry: selecting {entry}/{index} as requested by {text.Print()}");
                        return true;
                    }
                }
            }
            else
            {
                RethrottleGeneric();
            }
            return false;
        }

        internal static unsafe bool IsSelectItemEnabled(AddonSelectString* addon, int index)
        {
            var step1 = (AtkTextNode*)addon->AtkUnitBase
                        .UldManager.NodeList[2]
                        ->GetComponent()->UldManager.NodeList[index + 1]
                        ->GetComponent()->UldManager.NodeList[3];
            return GenericHelpers.IsSelectItemEnabled(step1);
        }

        internal static unsafe List<string> GetEntries(AddonSelectString* addon)
        {
            var list = new List<string>();
            for (var i = 0; i < addon->PopupMenu.PopupMenu.EntryCount; i++)
            {
                list.Add(MemoryHelper.ReadSeStringNullTerminated((nint)addon->PopupMenu.PopupMenu.EntryNames[i]).ExtractText());
            }
            return list;
        }

        internal static bool GenericThrottle => EzThrottler.Throttle($"AutomatonGenericThrottle", 200);
        internal static void RethrottleGeneric(int num) => EzThrottler.Throttle($"AutomatonGenericThrottle", num, true);
        internal static void RethrottleGeneric() => EzThrottler.Throttle($"AutomatonGenericThrottle", 200, true);

        internal static unsafe bool IsLoading() => (GenericHelpers.TryGetAddonByName<AtkUnitBase>("FadeBack", out var fb) && fb->IsVisible) || (GenericHelpers.TryGetAddonByName<AtkUnitBase>("FadeMiddle", out var fm) && fm->IsVisible);

        public bool IsInDuty() => Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] ||
                                Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty56] ||
                                Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty95] ||
                                Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundToDuty97];
    }
}
