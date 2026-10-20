using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace BagSense;

public sealed partial class BagWindow
{
    private int rewardFilter;
    internal Func<uint, Vector2, bool>? PreviewImage { get; set; }

    private string RewardType(string kind) => kind switch
    {
        "Mount" => T("МАУНТ · средство передвижения", "MOUNT · rideable companion"),
        "Minion" => T("МИНЬОН · спутник", "MINION · companion"),
        "Music" => T("МУЗЫКА · оркестрион", "MUSIC · orchestrion roll"),
        "Emote" => T("ЭМОЦИЯ", "EMOTE"), "Hairstyle" => T("ПРИЧЁСКА", "HAIRSTYLE"),
        "Card" => T("КАРТА TRIPLE TRIAD", "TRIPLE TRIAD CARD"), "Equipment" => T("ЭКИПИРОВКА", "EQUIPMENT"),
        "Housing" => T("МЕБЕЛЬ · украшение дома", "HOUSING · furnishing"), "Dye" => T("КРАСКА", "DYE"),
        "Material" => T("МАТЕРИАЛ", "MATERIAL"), "Accessory" => T("МОДНЫЙ АКСЕССУАР", "FASHION ACCESSORY"),
        "Barding" => T("БРОНЯ ДЛЯ ЧОКОБО", "CHOCOBO BARDING"), "Glasses" => T("ОЧКИ", "FACEWEAR"),
        "Portrait" => T("ОФОРМЛЕНИЕ ПОРТРЕТА", "PORTRAIT DESIGN"),
        _ => T("ПРЕДМЕТ", "ITEM")
    };

    private string RewardHelp(string kind) => kind switch
    {
        "Mount" => T("Используй полученный предмет в сумке один раз: маунт появится в Mount Guide.", "Use the received item once from your inventory to add this mount to Mount Guide."),
        "Minion" => T("Используй предмет в сумке: спутник появится в Minion Guide.", "Use this item from your inventory to add the companion to Minion Guide."),
        "Music" => T("Используй ноты в сумке, чтобы добавить композицию в оркестрион.", "Use the roll to add its music to your orchestrion collection."),
        "Emote" => T("Используй пособие в сумке, чтобы открыть новую эмоцию.", "Use the manual from your inventory to learn a new emote."),
        "Hairstyle" => T("Используй предмет, затем выбери причёску у эстетиста.", "Use the item, then choose the hairstyle at the aesthetician."),
        "Equipment" => T("Предмет для экипировки или гламура. Проверь профессию и характеристики перед обменом.", "Equipment for combat or glamour. Check its job and attributes before exchanging."),
        "Housing" => T("Украшение для жилья. Проверь тип размещения в описании.", "A housing furnishing. Check its placement type in the description."),
        "Material" => T("Расходный материал для создания или улучшения предметов. Это не готовая награда коллекции.", "A material for crafting or upgrades. This is not a completed collection reward."),
        "Card" => T("Используй карту в сумке, чтобы добавить её в колоду Triple Triad.", "Use the card to add it to your Triple Triad collection."),
        "Dye" => T("Расходуется при окраске подходящей экипировки.", "Consumed when dyeing compatible equipment."),
        _ => T("Ниже — описание из игры и полная стоимость получения.", "The game description and complete acquisition cost are shown below.")
    };

    private bool DrawGameImage(uint icon, Vector2 size)
    {
        if (icon == 0) return false;
        if (staticPreview) return PreviewImage?.Invoke(icon, size) == true;
        var texture = Plugin.Textures.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrDefault();
        if (texture == null) return false;
        float scale = Math.Min(size.X / texture.Width, size.Y / texture.Height);
        ImGui.Image(texture.Handle, new(texture.Width * scale, texture.Height * scale));
        return true;
    }

    private void DrawRewardCard(ItemFacts reward, uint count, InventorySnapshot snapshot)
    {
        var details = reward.Reward ?? new RewardDetails(reward.Equipment ? "Equipment" : "Item", reward.Name, reward.Description);
        ImGui.Spacing();
        ImGui.BeginGroup();
        bool image = details.PreviewIcon > 0 && DrawGameImage(details.PreviewIcon, new(170 * S, 124 * S));
        if (!image && !DrawGameImage(reward.Icon, new(52 * S, 52 * S))) ImGui.Dummy(new(52 * S, 52 * S));
        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextColored(Teal, RewardType(details.Kind));
        ImGui.TextWrapped(details.Name);
        if (details.Name != reward.Name) ImGui.TextDisabled(T("Предмет: ", "Item: ") + reward.Name);
        ImGui.TextWrapped(T($"Получишь: {count} шт.", $"You receive: {count}"));
        var unlocked = RewardPolicy.Unlocked(snapshot, reward.Id);
        if (unlocked.HasValue) ImGui.TextColored(unlocked.Value ? Muted : Gold, unlocked.Value
            ? T("Уже есть в коллекции", "Already in your collection") : T("Ещё нет в коллекции", "Not yet in your collection"));
        else if (details.Kind is "Mount" or "Minion" or "Music" or "Card" or "Emote" or "Hairstyle")
            ImGui.TextDisabled(T("Коллекция: статус пока не прочитан", "Collection: status not yet available"));
        ImGui.EndGroup();
        ImGui.EndGroup();
        ImGui.TextWrapped(RewardHelp(details.Kind));
        if (details.Detail.Length > 0) ImGui.TextWrapped(details.Detail);
        if (details.Description.Length > 0) ImGui.TextWrapped(details.Description.Length > 220 ? details.Description[..220] + "…" : details.Description);
        if (details.Description.Length > 220 && ImGui.CollapsingHeader(T("Полное описание награды", "Full reward description") + "###RewardDescription" + reward.Id))
            ImGui.TextWrapped(details.Description);
    }
}
