using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace EorzeaCompass;

internal sealed partial class CompassWindow
{
    private int atlasMode;
    private bool atlasTabRequest;
    private int ownedFilter;
    private string atlasSource = "";
    private static readonly string[] CollectionKinds = ["", "Mount", "Minion", "Music", "Emote", "Hairstyle", "Card", "Portrait", "Accessory", "Barding", "Glasses", "Unlock", "Record", "FieldNote"];
    private string CollectionLabel(string kind) => kind switch
    {
        "Mount" => T("Маунты", "Mounts"),
        "Minion" => T("Миньоны", "Minions"),
        "Music" => T("Музыка", "Music"),
        "Emote" => T("Эмоции", "Emotes"),
        "Hairstyle" => T("Причёски", "Hairstyles"),
        "Card" => T("Карты", "Cards"),
        "Portrait" => T("Портреты", "Portraits"),
        "Accessory" => T("Аксессуары", "Accessories"),
        "Barding" => T("Броня чокобо", "Chocobo barding"),
        "Glasses" => T("Очки", "Glasses"),
        "Record" => T("Записи Occult", "Occult records"),
        "FieldNote" => T("Полевые записи", "Field notes"),
        "Unlock" => T("Другие открытия", "Other unlocks"),
        _ => T("Все типы", "All types"),
    };
    private string CollectedLabel(QuestReward r) => plugin.CollectionState(r) switch
    { "collected" => T("Получено", "Collected"), "missing" => T("Ещё не получено", "Not collected"), _ => T("Нет данных о получении", "Collection unknown") };
    private string RewardType(QuestReward r) => r.Kind switch
    { RewardKind.Vendor => T("Покупка / обмен", "Purchase / exchange"), RewardKind.Choice => T("На выбор", "Choose one"), _ => T("Награда за квест", "Quest reward") };
    private string SourceName(QuestReward r) => r.Vendor?.Source ?? plugin.QuestName(r.QuestId);
    private Vector4 CollectedColor(QuestReward r) => plugin.CollectionState(r) == "collected" ? Accent : plugin.CollectionState(r) == "missing" ? Gold : Muted;

    private void ResetAtlas()
    { atlasStory = atlasSource = search = ""; rewardFilter = rewardCategory = ownedFilter = 0; selectedReward = -1; }

    private void DrawAtlas()
    {
        ImGui.TextColored(Accent, T("Атлас наград", "Reward atlas"));
        ImGui.SameLine(0, 20 * S);
        if (ImGui.SmallButton(T("Сбросить фильтры", "Reset filters"))) ResetAtlas();
        var requestedMode = atlasMode;
        if (ImGui.BeginTabBar("atlas-mode"))
        {
            if (ImGui.BeginTabItem(T("Коллекции", "Collections"), atlasTabRequest && requestedMode == 0 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { if (atlasMode != 0) { atlasMode = 0; rewardCategory = rewardFilter = 0; } ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem(T("Все награды", "All rewards"), atlasTabRequest && requestedMode == 1 ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None))
            { if (atlasMode != 1) { atlasMode = 1; rewardCategory = rewardFilter = 0; } ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
        atlasTabRequest = false;
        if (plugin.Catalog!.Warnings.Count > 0 && ImGui.CollapsingHeader(T("Часть магазинов требует обновления каталога", "Some shops need a catalog update")))
            ImGui.TextWrapped(string.Join(", ", plugin.Catalog.Warnings.Distinct()));
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##reward-search", T("Предмет, валюта, общество или квест…", "Item, currency, society or quest…"), ref search, 200);
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(width * .38f);
        var sourceLabel = atlasSource switch { "" => T("Все источники", "All sources"), "quest" => T("Только квесты", "Quests only"), "Society" => T("Все общества", "All societies"), "Exploration" => T("Исследовательские зоны", "Exploration zones"), _ => atlasSource };
        if (ImGui.BeginCombo("##atlas-source", sourceLabel))
        {
            if (ImGui.Selectable(T("Все источники", "All sources"), atlasSource.Length == 0)) atlasSource = "";
            if (ImGui.Selectable(T("Только квесты", "Quests only"), atlasSource == "quest")) atlasSource = "quest";
            if (ImGui.Selectable(T("Все общества", "All societies"), atlasSource == "Society")) atlasSource = "Society";
            if (ImGui.Selectable(T("Все исследовательские зоны", "All exploration zones"), atlasSource == "Exploration")) atlasSource = "Exploration";
            foreach (var source in plugin.Catalog!.Rewards.Where(r => r.Vendor != null).Select(r => r.Vendor!.Source).Distinct().Order())
                if (ImGui.Selectable(source, atlasSource == source)) atlasSource = source;
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(width * .28f);
        var categories = atlasMode == 0 ? CollectionKinds.Select(CollectionLabel).ToArray()
            : new[] { T("Все типы предметов", "All item categories") }.Concat(plugin.Catalog!.Rewards.Select(r => r.Category).Distinct().Order()).ToArray();
        if (rewardCategory >= categories.Length) rewardCategory = 0;
        ImGui.Combo("##reward-category", ref rewardCategory, categories, categories.Length);
        ImGui.SameLine(); ImGui.SetNextItemWidth(-1);
        if (atlasMode == 0)
            ImGui.Combo("##collection-state", ref ownedFilter, new[] { T("Все состояния", "All states"), T("Ещё не получено", "Not collected"), T("Получено", "Collected"), T("Нет данных", "Unknown") }, 4);
        else
            ImGui.Combo("##quest-reward-state", ref rewardFilter, new[] { T("Все награды", "All rewards"), T("Покупка / обмен", "Purchase / exchange"), T("За квест", "Quest reward"), T("На выбор", "Choose one") }, 4);
        if (atlasStory.Length > 0)
        {
            ImGui.TextColored(Muted, plugin.Catalog!.Stories.First(s => s.Definition.Id == atlasStory).Definition.Name);
            ImGui.SameLine(); if (ImGui.SmallButton(T("Убрать цепочку", "Clear chain"))) atlasStory = "";
        }
        var catalog = plugin.Catalog!;
        var allowedStories = catalog.Stories.Where(s => expansion == uint.MaxValue || s.Definition.Expansion == expansion).Select(s => s.Definition.Id).ToHashSet();
        var baseRows = catalog.Rewards.Select((r, index) => (Reward: r, Index: index))
            .Where(x => atlasMode == 0 ? x.Reward.CollectionKind.Length > 0 : true)
            .Where(x => atlasStory.Length == 0 || x.Reward.StoryIds.Contains(atlasStory))
            .Where(x => expansion == uint.MaxValue || (x.Reward.Vendor is { } vendor ? vendor.Expansion == expansion : x.Reward.StoryIds.Any(allowedStories.Contains)))
            .Where(x => atlasSource.Length == 0 || atlasSource == "quest" && x.Reward.Kind != RewardKind.Vendor
                || x.Reward.Vendor is { } v && (v.Group == atlasSource || v.Source == atlasSource))
            .Where(x => rewardCategory == 0 || (atlasMode == 0 ? x.Reward.CollectionKind == CollectionKinds[rewardCategory] : x.Reward.Category == categories[rewardCategory]))
            .Where(x => search.Length == 0 || MatchesReward(x.Reward, search)).ToArray();
        var collection = baseRows.GroupBy(x => x.Reward.ItemId).Select(g => g.First()).ToArray();
        var rows = (atlasMode == 0 ? collection.Where(x => ownedFilter == 0 || plugin.CollectionState(x.Reward) == (ownedFilter == 1 ? "missing" : ownedFilter == 2 ? "collected" : "unknown"))
            : baseRows.Where(x => rewardFilter == 0 || rewardFilter == 3 && x.Reward.Kind == RewardKind.Choice
                || rewardFilter == 1 && x.Reward.Kind == RewardKind.Vendor
                || rewardFilter == 2 && x.Reward.Kind == RewardKind.Guaranteed)).ToArray();
        selectedReward = AtlasSelection.KeepVisible(selectedReward, catalog.Rewards, (atlasMode == 0 ? baseRows : rows).Select(x => x.Index).ToArray(), rows.Select(x => x.Index).ToArray());
        if (atlasMode == 0) DrawCollectionSummary(collection.Select(x => x.Reward).ToArray(), rows.Length);
        else ImGui.TextColored(Muted, T($"Наград: {rows.Length}", $"Rewards: {rows.Length}"));
        ImGui.Separator();
        DrawAtlasPanels(rows);
    }

    private bool MatchesReward(QuestReward r, string query) => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || SourceName(r).Contains(query, StringComparison.OrdinalIgnoreCase)
        || r.Vendor?.Npcs.Any(n => n.Contains(query, StringComparison.OrdinalIgnoreCase)) == true
        || r.Vendor?.Costs.Any(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) == true;

    private void DrawCollectionSummary(QuestReward[] rewards, int visible)
    {
        var collected = rewards.Count(r => plugin.CollectionState(r) == "collected");
        var unknown = rewards.Count(r => plugin.CollectionState(r) == "unknown");
        ImGui.TextColored(Accent, T($"Получено {collected} / {rewards.Length}", $"Collected {collected} / {rewards.Length}"));
        ImGui.SameLine();
        ImGui.TextColored(Muted, T($"Показано: {visible}" + (unknown > 0 ? $" · нет данных: {unknown}" : ""), $"Showing: {visible}" + (unknown > 0 ? $" · unknown: {unknown}" : "")));
        ImGui.ProgressBar(rewards.Length == 0 ? 0 : (float)collected / rewards.Length, new Vector2(-1, 4 * S), "");
    }

    private void DrawAtlasPanels((QuestReward Reward, int Index)[] rows)
    {
        if (ImGui.BeginChild("reward-list", new Vector2(ImGui.GetContentRegionAvail().X * .43f, 0), true))
        {
            foreach (var row in rows) DrawRewardRow(row.Reward, row.Index);
            if (rows.Length == 0) { ImGui.TextWrapped(T("Ничего не найдено", "No rewards found")); ImGui.TextWrapped(T("Попробуй изменить источник или состояние коллекции.", "Try a different source or collection state.")); }
        }
        ImGui.EndChild(); ImGui.SameLine();
        if (ImGui.BeginChild("reward-detail", Vector2.Zero, true))
        {
            if (selectedReward >= 0) DrawRewardDetail(plugin.Catalog!.Rewards[selectedReward]);
            else ImGui.TextWrapped(T("Выбери предмет слева.", "Choose an item on the left."));
        }
        ImGui.EndChild();
    }

    private void DrawRewardIcon(QuestReward r, float size)
    {
        if (Plugin.Textures != null && r.Icon != 0)
        {
            var icon = Plugin.Textures.GetFromGameIcon(new GameIconLookup(r.Icon)).GetWrapOrEmpty();
            ImGui.Image(icon.Handle, new Vector2(size, size) * S);
        }
        else ImGui.Dummy(new Vector2(size, size) * S);
    }

    private void DrawRewardRow(QuestReward r, int index)
    {
        ImGui.PushID(index);
        var start = ImGui.GetCursorPos(); var width = ImGui.GetContentRegionAvail().X;
        var summary = r.CollectionKind.Length > 0 ? CollectedLabel(r) : RewardType(r);
        var textWidth = Math.Max(40 * S, width - 58 * S);
        var height = Math.Max(54 * S, ImGui.CalcTextSize(r.Name, false, textWidth).Y + ImGui.CalcTextSize(summary, false, textWidth).Y + 12 * S);
        if (!ImGui.IsRectVisible(new Vector2(width, height))) { ImGui.Dummy(new Vector2(width, height)); ImGui.Separator(); ImGui.PopID(); return; }
        var isSelected = selectedReward >= 0 && selectedReward < plugin.Catalog!.Rewards.Count
            && (atlasMode == 0 ? plugin.Catalog.Rewards[selectedReward].ItemId == r.ItemId : selectedReward == index);
        if (ImGui.Selectable("##reward", isSelected, ImGuiSelectableFlags.None, new Vector2(0, height))) selectedReward = index;
        var after = ImGui.GetCursorPos();
        ImGui.SetCursorPos(start + new Vector2(5, 6) * S); DrawRewardIcon(r, 32);
        ImGui.SetCursorPos(start + new Vector2(46, 4) * S);
        ImGui.PushTextWrapPos(start.X + width - 8 * S); ImGui.TextUnformatted(r.Name);
        ImGui.TextColored(r.CollectionKind.Length > 0 ? CollectedColor(r) : Muted, summary);
        ImGui.PopTextWrapPos(); ImGui.SetCursorPos(after); ImGui.Separator(); ImGui.PopID();
    }

    private void DrawRewardDetail(QuestReward r)
    {
        DrawRewardIcon(r, 42); ImGui.SameLine();
        ImGui.BeginGroup(); ImGui.TextWrapped(r.Name);
        ImGui.TextColored(r.CollectionKind.Length > 0 ? CollectedColor(r) : Muted, r.CollectionKind.Length > 0 ? CollectedLabel(r) : RewardType(r));
        ImGui.EndGroup();
        ImGui.TextColored(Muted, r.CollectionKind.Length > 0 ? CollectionLabel(r.CollectionKind) : r.Category);
        ImGui.Separator();
        if (r.CollectionKind.Length > 0)
        {
            if (plugin.CollectionState(r) == "missing") ImGui.TextWrapped(T("Предмет ещё не зарегистрирован в коллекции. Если он уже в сумке, используй его.", "Not registered in your collection yet. If the item is in your bag, use it to unlock it."));
            else if (plugin.CollectionState(r) == "unknown") ImGui.TextWrapped(T("Получение пока не подтверждено. Для этого типа может отсутствовать надёжная проверка.", "Collection is unconfirmed. A reliable check may be unavailable for this item type."));
        }
        if (r.Vendor is { } offer) DrawVendorOffer(offer);
        else DrawQuestRewardSource(r);
        var alternatives = plugin.Catalog!.Rewards.Select((reward, index) => (reward, index)).Where(x => x.reward.ItemId == r.ItemId)
            .DistinctBy(x => x.reward.Vendor == null ? $"quest:{x.reward.QuestId}:{x.reward.Kind}" : $"shop:{x.reward.Vendor.SourceId}:{x.reward.Vendor.ShopId}").ToArray();
        if (alternatives.Length > 1 && ImGui.CollapsingHeader(T($"Другие способы получения ({alternatives.Length - 1})", $"Other sources ({alternatives.Length - 1})")))
            foreach (var x in alternatives.Where(x => x.reward != r))
                if (ImGui.Selectable(SourceName(x.reward) + "##source-" + x.index))
                { selectedReward = x.index; atlasSource = atlasStory = ""; rewardFilter = 0; expansion = uint.MaxValue; search = r.Name; }
        if (r.StoryIds.Length > 0 && ImGui.CollapsingHeader(T("Связанные цепочки", "Related quest chains")))
            foreach (var id in r.StoryIds)
            {
                var story = plugin.Catalog.Stories.First(s => s.Definition.Id == id);
                if (ImGui.Selectable(story.Definition.Name + "##" + id)) { selectedStory = id; section = 1; storyKind = storyFilter = 0; expansion = story.Definition.Expansion; search = ""; }
            }
    }

    private void DrawVendorOffer(VendorOffer offer)
    {
        ImGui.TextColored(Gold, T("ГДЕ КУПИТЬ", "WHERE TO BUY"));
        ImGui.TextWrapped(offer.Source);
        ImGui.TextWrapped(string.Join(", ", offer.Npcs.Distinct()));
        ImGui.TextColored(Muted, T("Стоимость за покупку", "Cost per purchase"));
        foreach (var cost in offer.Costs) ImGui.TextWrapped($"{cost.Count:N0} × {cost.Name}" + (cost.Collectability > 0 ? T($" · коллекционная ценность ≥ {cost.Collectability}", $" · collectability ≥ {cost.Collectability}") : ""));
        ImGui.Separator();
        ImGui.TextColored(Gold, T("УСЛОВИЯ ПОКУПКИ", "PURCHASE REQUIREMENTS"));
        var state = CollectionRules.Conditions(offer.Requirements.Select(plugin.ShopCondition));
        ImGui.TextWrapped(state switch { "locked" => T("Есть невыполненные условия", "Some requirements are incomplete"), "unknown" => T("Часть условий нужно проверить у NPC", "Some conditions need checking at the NPC"), _ => T("Указанные условия выполнены. Нужна валюта.", "Listed requirements met. Currency is still required.") });
        if (offer.Requirements.Length > 0 && ImGui.CollapsingHeader(T($"Требования ({offer.Requirements.Length})", $"Requirements ({offer.Requirements.Length})")))
            foreach (var rule in offer.Requirements)
            {
                var met = plugin.ShopCondition(rule);
                var prefix = met == true ? "[+] " : met == false ? "[ ] " : "[?] ";
                var label = rule.Kind == "unknown" ? T("Дополнительный доступ / условия у NPC", "Additional access / NPC conditions")
                    : rule.Kind == "achievement" ? T("Достижение: ", "Achievement: ") + rule.Name
                    : rule.Kind.StartsWith("duty", StringComparison.Ordinal) ? T("Активность: ", "Duty: ") + rule.Name : T("Квест: ", "Quest: ") + rule.Name;
                ImGui.TextWrapped(prefix + label);
                if (rule.Kind == "achievement" && met == null) ImGui.TextWrapped(T("Открой окно достижений в игре для проверки.", "Open the in-game Achievements window to check."));
            }
        if (offer.Locations.Length > 0)
        {
            ImGui.Separator();
            foreach (var location in offer.Locations.Take(1))
            {
                ImGui.TextWrapped($"{location.Zone} · X: {location.X:F1}, Y: {location.Y:F1}");
                if (ImGui.Button(T("Показать торговца на карте", "Show vendor on map"), new Vector2(-1, 0))) new QuestLocation(location.Npc, location.Zone, location.Territory, location.Map, location.X, location.Y).Open();
            }
            if (offer.Locations.Length > 1 && ImGui.CollapsingHeader(T("Другие точки торговли", "Other vendor locations")))
                foreach (var location in offer.Locations.Skip(1))
                    if (ImGui.Selectable($"{location.Zone} · {location.X:F1}, {location.Y:F1}##{location.Map}-{location.Npc}")) new QuestLocation(location.Npc, location.Zone, location.Territory, location.Map, location.X, location.Y).Open();
        }
        else ImGui.TextWrapped(T("Координаты торговца пока недоступны.", "Vendor coordinates are not available yet."));
    }

    private void DrawQuestRewardSource(QuestReward r)
    {
        ImGui.TextColored(Gold, T("НАГРАДА ЗА КВЕСТ", "QUEST REWARD"));
        ImGui.TextWrapped(plugin.QuestName(r.QuestId));
        ImGui.TextColored(Muted, $"{RewardType(r)} · ×{r.Count}");
        if (r.CollectionKind.Length == 0 || plugin.CollectionState(r) != "collected")
            ImGui.TextWrapped(RewardCatalog.Status(r, plugin.QuestProgress.GetValueOrDefault(r.QuestId)) switch
            {
                "choice-completed" => T("Квест выполнен. История выбора предмета недоступна.", "Quest completed. Past reward choices are unavailable."),
                "quest-completed" => T("Квест выполнен; наличие предмета в сумке не проверяется.", "Quest completed; current bag contents are not checked."),
                "pending" => r.Kind == RewardKind.Choice ? T("Заверши квест и выбери этот предмет.", "Complete the quest and choose this reward.") : T("Заверши квест, чтобы получить предмет.", "Complete the quest to receive this item."),
                _ => T("Прогресс квеста неизвестен.", "Quest progress is unknown.")
            });
        var location = locate(r.QuestId);
        if (location != null) { ImGui.TextWrapped($"{location.Npc} · {location.Zone}"); if (ImGui.Button(T("NPC на карте", "Quest giver on map"), new Vector2(-1, 0))) location.Open(); }
    }
}
