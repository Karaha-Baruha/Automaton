﻿using Dalamud.Game;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Lumina.Excel;
using System.Diagnostics.CodeAnalysis;

namespace Automaton.Utilities;
public static class Excel
{
    public static ExcelSheet<T> GetSheet<T>(ClientLanguage? language = null) where T : ExcelRow
        => Svc.Data.GetExcelSheet<T>(language ?? Svc.ClientState.ClientLanguage)!;

    public static uint GetRowCount<T>() where T : ExcelRow
        => GetSheet<T>().RowCount;

    public static T? GetRow<T>(uint rowId, uint subRowId = uint.MaxValue, ClientLanguage? language = null) where T : ExcelRow
        => GetSheet<T>(language).GetRow(rowId, subRowId);

    public static T? FindRow<T>(Func<T?, bool> predicate) where T : ExcelRow
        => GetSheet<T>().FirstOrDefault(predicate, null);

    public static IEnumerable<T> FindRows<T>(Func<T?, bool> predicate) where T : ExcelRow
        => GetSheet<T>().Where(predicate);

    // https://github.com/Koenari/HimbeertoniRaidTool/blob/b28313e6d62de940acc073f203e3032e846bfb13/HimbeertoniRaidTool/UI/ImGuiHelper.cs#L188
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString(), flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string, bool> searchPredicate, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString(), searchPredicate, flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString(), preFilter, flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string, bool> searchPredicate, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString(), searchPredicate, preFilter, flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, toName, (t, s) => toName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase), flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName, Func<T, string, bool> searchPredicate, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, toName, searchPredicate, _ => true, flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
        => ExcelSheetCombo(id, out selected, getPreview, toName, (t, s) => toName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase), preFilter, flags);

    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T? selected, Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName, Func<T, string, bool> searchPredicate, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : ExcelRow
    {
        var sheet = GetSheet<T>();
        if (sheet is null)
        {
            selected = null;
            return false;
        }
        return SearchableCombo(id, out selected, getPreview(sheet), sheet, toName, searchPredicate, preFilter, flags);
    }

    private static string _search = string.Empty;
    private static HashSet<object>? _filtered;
    private static int _hoveredItem;
    private static readonly Dictionary<string, (bool toogle, bool wasEnterClickedLastTime)> _comboDic = [];

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, IEnumerable<T> possibilities, Func<T, string> toName, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
        => SearchableCombo(id, out selected, preview, possibilities, toName, (p, s) => toName.Invoke(p).Contains(s, StringComparison.InvariantCultureIgnoreCase), flags);

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, IEnumerable<T> possibilities, Func<T, string> toName, Func<T, string, bool> searchPredicate, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
        => SearchableCombo(id, out selected, preview, possibilities, toName, searchPredicate, _ => true, flags);

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview, IEnumerable<T> possibilities, Func<T, string> toName, Func<T, string, bool> searchPredicate, Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
    {

        _comboDic.TryAdd(id, (false, false));
        (var toggle, var wasEnterClickedLastTime) = _comboDic[id];
        selected = default;
        if (!ImGui.BeginCombo(id + (toggle ? "##x" : ""), preview, flags)) return false;

        if (wasEnterClickedLastTime || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            toggle = !toggle;
            _search = string.Empty;
            _filtered = null;
        }
        var enterClicked = ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
        wasEnterClickedLastTime = enterClicked;
        _comboDic[id] = (toggle, wasEnterClickedLastTime);
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            _hoveredItem--;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            _hoveredItem++;
        _hoveredItem = Math.Clamp(_hoveredItem, 0, Math.Max(_filtered?.Count - 1 ?? 0, 0));
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            _search = string.Empty;
            _filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref _search, 128))
            _filtered = null;

        if (_filtered == null)
        {
            _filtered = possibilities.Where(preFilter).Where(s => searchPredicate(s, _search)).Cast<object>().ToHashSet();
            _hoveredItem = 0;
        }

        var i = 0;
        foreach (var row in _filtered.Cast<T>())
        {
            var hovered = _hoveredItem == i;
            using (ImRaii.PushId(i))
            {
                if (ImGui.Selectable(toName(row), hovered) || enterClicked && hovered)
                {
                    selected = row;
                    ImGui.PopID();
                    ImGui.EndCombo();
                    return true;
                }
            }
            i++;
        }

        ImGui.EndCombo();
        return false;
    }
}
