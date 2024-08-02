using Dalamud.Interface.Components;
using Dalamud.Memory;
using ECommons;
using ECommons.Automation;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace Automaton.Features;

public class AutoSelectGardeningConfiguration
{
    public uint SelectedSoil = 0;
    public uint SelectedSeed = 0;

    public bool IncludeFertilzing = false;
    public uint SelectedFertilizer = 0;

    public bool AutoConfirm = false;
    public bool Fallback = false;
    public bool OnlyShowInventoryItems = false;
}

[Tweak]
public unsafe class AutoSelectGardening : Tweak<AutoSelectGardeningConfiguration>
{
    public override string Name => "Auto-select Gardening Soil/Seeds";
    public override string Description => "Automatically fill in gardening windows with seeds and soil. This has a fallback option that Pandora doesn't.";

    public Dictionary<uint, Item> Seeds { get; set; } = null!;
    public Dictionary<uint, Item> Soils { get; set; } = null!;
    public Dictionary<uint, Item> Fertilizers { get; set; } = null!;
    public Dictionary<uint, Addon> AddonText { get; set; } = null!;

    private bool Fertilized { get; set; } = false;
    private List<int> SlotsFilled { get; set; } = [];

    public override void Enable()
    {
        Seeds = GetSheet<Item>().Where(x => x.ItemUICategory.Row == 82 && x.FilterGroup == 20).ToDictionary(x => x.RowId, x => x);
        Soils = GetSheet<Item>().Where(x => x.ItemUICategory.Row == 82 && x.FilterGroup == 21).ToDictionary(x => x.RowId, x => x);
        Fertilizers = GetSheet<Item>().Where(x => x.ItemUICategory.Row == 82 && x.FilterGroup == 22).ToDictionary(x => x.RowId, x => x);
        AddonText = GetSheet<Addon>().ToDictionary(x => x.RowId, x => x);
        Svc.Framework.Update += RunFeature;
    }

    public override void Disable()
    {
        Svc.Framework.Update -= RunFeature;
        Seeds = [];
        Soils = [];
        AddonText = [];
        Fertilizers = [];
        SlotsFilled.Clear();
    }

    public override void DrawConfig()
    {
        ImGuiX.DrawSection("Configuration");

        if (Utils.AnyNull(Seeds, Soils, Fertilizers)) return;

        ImGui.Checkbox("Show Only Inventory Items", ref Config.OnlyShowInventoryItems);

        var invSoil = Config.OnlyShowInventoryItems ? Soils.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Value.RowId) > 0).ToArray() : [.. Soils];
        var invSeeds = Config.OnlyShowInventoryItems ? Seeds.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Value.RowId) > 0).ToArray() : [.. Seeds];
        var invFert = Config.OnlyShowInventoryItems ? Fertilizers.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Value.RowId) > 0).ToArray() : [.. Fertilizers];

        var soilPrev = Config.SelectedSoil == 0 ? "" : Soils[Config.SelectedSoil].Name.ExtractText();
        if (ImGui.BeginCombo("Soil", soilPrev))
        {
            if (ImGui.Selectable("", Config.SelectedSoil == 0))
            {
                Config.SelectedSoil = 0;
            }
            foreach (var soil in invSoil)
            {
                var selected = ImGui.Selectable(soil.Value.Name.ExtractText(), Config.SelectedSoil == soil.Key);

                if (selected)
                {
                    Config.SelectedSoil = soil.Key;
                }
            }

            ImGui.EndCombo();
        }

        var seedPrev = Config.SelectedSeed == 0 ? "" : Seeds[Config.SelectedSeed].Name.ExtractText();
        if (ImGui.BeginCombo("Seed", seedPrev))
        {
            if (ImGui.Selectable("", Config.SelectedSeed == 0))
            {
                Config.SelectedSeed = 0;
            }
            foreach (var seed in invSeeds)
            {
                var selected = ImGui.Selectable(seed.Value.Name.ExtractText(), Config.SelectedSeed == seed.Key);

                if (selected)
                {
                    Config.SelectedSeed = seed.Key;
                }
            }

            ImGui.EndCombo();
        }

        ImGui.Checkbox("Include Fertilizing", ref Config.IncludeFertilzing);

        if (Config.IncludeFertilzing)
        {
            var fertPrev = Config.SelectedFertilizer == 0 ? "" : Fertilizers[Config.SelectedFertilizer].Name.ExtractText();
            if (ImGui.BeginCombo("Fertilizer", fertPrev))
            {
                if (ImGui.Selectable("", Config.SelectedFertilizer == 0))
                {
                    Config.SelectedFertilizer = 0;
                }
                foreach (var fert in invFert)
                {
                    var selected = ImGui.Selectable(fert.Value.Name.ExtractText(), Config.SelectedFertilizer == fert.Key);

                    if (selected)
                    {
                        Config.SelectedFertilizer = fert.Key;
                    }
                }

                ImGui.EndCombo();
            }
        }

        ImGui.Checkbox("Soil/Seed Fallback", ref Config.Fallback);
        ImGuiComponents.HelpMarker("When enabled, this will select the first soil/seed found in your inventory if the\nprimary ones chosen are not found.");

        ImGui.Checkbox("Auto Confirm", ref Config.AutoConfirm);
    }

    private void RunFeature(IFramework framework)
    {
        if (!Player.Available) return;
        if (Config.IncludeFertilzing && Svc.GameGui.GetAddonByName("InventoryExpansion") != nint.Zero && !Fertilized)
        {
            if (Config.SelectedFertilizer == 0) goto SoilSeeds;
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("InventoryExpansion");

            if (addon->IsVisible)
            {
                if (addon->AtkValuesCount <= 5) return;
                var fertilizeText = addon->AtkValues[5];
                var text = MemoryHelper.ReadSeStringNullTerminated(new nint(fertilizeText.String));
                if (text.ExtractText() == AddonText[6417].Text.ExtractText())
                {
                    var im = InventoryManager.Instance();
                    var inv1 = im->GetInventoryContainer(InventoryType.Inventory1);
                    var inv2 = im->GetInventoryContainer(InventoryType.Inventory2);
                    var inv3 = im->GetInventoryContainer(InventoryType.Inventory3);
                    var inv4 = im->GetInventoryContainer(InventoryType.Inventory4);

                    InventoryContainer*[] container =
                    [
                        inv1, inv2, inv3, inv4
                    ];

                    foreach (var cont in container)
                    {
                        for (var i = 0; i < cont->Size; i++)
                        {
                            if (cont->GetInventorySlot(i)->ItemId == Config.SelectedFertilizer)
                            {
                                var item = cont->GetInventorySlot(i);

                                var ag = AgentInventoryContext.Instance();
                                ag->OpenForItemSlot(cont->Type, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonId());
                                var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                if (contextMenu == null) return;
                                Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                Fertilized = true;
                                return;
                            }
                        }
                    }

                    return;
                }
            }
            else
            {
                goto SoilSeeds;
            }
        }
        else
        {
            Fertilized = false;
        }

    SoilSeeds:
        if (Svc.GameGui.GetAddonByName("HousingGardening") != nint.Zero)
        {
            if (Config.SelectedSeed == 0 && Config.SelectedSoil == 0) return;
            var invSoil = Soils.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Value.RowId) > 0).Select(x => x.Key).ToList();
            var invSeeds = Seeds.Where(x => InventoryManager.Instance()->GetInventoryItemCount(x.Value.RowId) > 0).Select(x => x.Key).ToList();

            var im = InventoryManager.Instance();
            var inv1 = im->GetInventoryContainer(InventoryType.Inventory1);
            var inv2 = im->GetInventoryContainer(InventoryType.Inventory2);
            var inv3 = im->GetInventoryContainer(InventoryType.Inventory3);
            var inv4 = im->GetInventoryContainer(InventoryType.Inventory4);

            InventoryContainer*[] container =
            [
                        inv1, inv2, inv3, inv4
            ];

            var soilIndex = 0;
            foreach (var cont in container)
            {
                for (var i = 0; i < cont->Size; i++)
                {
                    if (invSoil.Any(x => cont->GetInventorySlot(i)->ItemId == x))
                    {
                        var item = cont->GetInventorySlot(i);
                        if (item->ItemId == Config.SelectedSoil)
                            goto SetSeed;
                        else
                            soilIndex++;
                    }
                }
            }
            if (Config.Fallback)
            {
                soilIndex = 0;
                foreach (var cont in container)
                {
                    for (var i = 0; i < cont->Size; i++)
                    {
                        if (invSoil.Any(x => cont->GetInventorySlot(i)->ItemId == x))
                        {
                            var item = cont->GetInventorySlot(i);
                            if (item->ItemId == invSoil[0])
                                goto SetSeed;
                            else
                                soilIndex++;
                        }
                    }
                }
            }

        SetSeed:
            var seedIndex = 0;
            foreach (var cont in container)
            {
                for (var i = 0; i < cont->Size; i++)
                {
                    if (invSeeds.Any(x => cont->GetInventorySlot(i)->ItemId == x))
                    {
                        var item = cont->GetInventorySlot(i);
                        if (item->ItemId == Config.SelectedSeed)
                            goto ClickItem;
                        else
                            seedIndex++;
                    }
                }
            }
            if (Config.Fallback)
            {
                seedIndex = 0;
                foreach (var cont in container)
                {
                    for (var i = 0; i < cont->Size; i++)
                    {
                        if (invSeeds.Any(x => cont->GetInventorySlot(i)->ItemId == x))
                        {
                            var item = cont->GetInventorySlot(i);

                            if (item->ItemId == invSeeds[0])
                                goto ClickItem;
                            else
                                seedIndex++;
                        }
                    }
                }
            }

        ClickItem:
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("HousingGardening");

            if (!TaskManager.IsBusy)
            {
                if (soilIndex != -1)
                {
                    if (SlotsFilled.Contains(1)) TaskManager.Abort();
                    if (SlotsFilled.Contains(1)) return;
                    TaskManager.EnqueueDelay(100);
                    TaskManager.Enqueue(() => TryClickItem(addon, 1, soilIndex));
                }

                if (seedIndex != -1)
                {
                    if (SlotsFilled.Contains(2)) TaskManager.Abort();
                    if (SlotsFilled.Contains(2)) return;
                    TaskManager.EnqueueDelay(100);
                    TaskManager.Enqueue(() => TryClickItem(addon, 2, seedIndex));
                }

                if (Config.AutoConfirm)
                {
                    TaskManager.EnqueueDelay(100);
                    TaskManager.Enqueue(() => Callback.Fire(addon, false, 0, 0, 0, 0, 0));
                    TaskManager.Enqueue(ConfirmYesNo);
                }
            }
        }
        else
        {
            SlotsFilled.Clear();
            TaskManager.Abort();
        }
    }

    private bool? TryClickItem(AtkUnitBase* addon, int i, int itemIndex)
    {
        if (SlotsFilled.Contains(i)) return true;

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextIconMenu", 1);

        if (contextMenu is null || !contextMenu->IsVisible)
        {
            var slot = i - 1;

            Svc.Log.Debug($"{slot}");
            var values = stackalloc AtkValue[5];
            values[0] = new AtkValue()
            {
                Type = AtkValueType.Int,
                Int = 2
            };
            values[1] = new AtkValue()
            {
                Type = AtkValueType.UInt,
                UInt = (uint)slot
            };
            values[2] = new AtkValue()
            {
                Type = AtkValueType.Int,
                Int = 0
            };
            values[3] = new AtkValue()
            {
                Type = AtkValueType.Int,
                Int = 0
            };
            values[4] = new AtkValue()
            {
                Type = AtkValueType.UInt,
                UInt = 1
            };

            addon->FireCallback(5, values);
            CloseItemDetail();
            return false;
        }
        else
        {
            var value = (uint)(i == 1 ? 27405 : 27451);
            var values = stackalloc AtkValue[5];
            values[0] = new AtkValue()
            {
                Type = AtkValueType.Int,
                Int = 0
            };
            values[1] = new AtkValue()
            {
                Type = AtkValueType.Int,
                Int = itemIndex
            };
            values[2] = new AtkValue()
            {
                Type = AtkValueType.UInt,
                UInt = value
            };
            values[3] = new AtkValue()
            {
                Type = AtkValueType.UInt,
                UInt = 0
            };
            values[4] = new AtkValue()
            {
                Type = AtkValueType.Int,
                UInt = 0
            };

            contextMenu->FireCallback(5, values, true);
            Svc.Log.Debug($"Filled slot {i}");
            SlotsFilled.Add(i);
            return true;
        }
    }

    private bool CloseItemDetail()
    {
        var itemDetail = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ItemDetail", 1);
        if (itemDetail is null || !itemDetail->IsVisible) return false;

        var values = stackalloc AtkValue[1];
        values[0] = new AtkValue()
        {
            Type = AtkValueType.Int,
            Int = -1
        };

        itemDetail->FireCallback(1, values);
        return true;
    }

    internal static bool ConfirmYesNo()
    {
        if (Svc.Condition[ConditionFlag.Occupied39]) return false;
        var hg = (AtkUnitBase*)Svc.GameGui.GetAddonByName("HousingGardening");
        if (hg == null) return false;

        if (hg->IsVisible && TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) && addon->AtkUnitBase.IsVisible && addon->YesButton->IsEnabled && addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible())
        {
            new AddonMaster.SelectYesno((nint)addon).Yes();
            return true;
        }

        return false;
    }
}
