namespace BagSense;

public enum ContainerKind { Bag, Armoury, Equipped }
public enum PlanAction { Use, Vendor, Desynthesize, ExchangeReview, ManualReview }
public enum AdviceKind { Protected, Reserved, Learn, Vendor, Gear, Exchange, Review, Deferred }
public enum Reason
{
    UnknownItem, AlwaysKeep, Equipped, Gearset, GearsetsUnknown, Indisposable, SpecialInstance,
    GoalReserve, FullReserve, Learn, AlreadyLearned, VendorSource, GearReview, ExchangeUses, RecipeUses, UnknownUse, Deferred, QuestItem, GilToken,
}

public sealed record InventoryStack(uint ItemId, ContainerKind Container, int Slot, int Quantity,
    bool Hq = false, bool Collectable = false, bool ValuableInstance = false, uint Collectability = 0);
public sealed record NpcLocation(uint NpcId, string Npc, string Zone, uint Territory, uint Map, float X, float Y)
{
    public string Key => FormattableString.Invariant($"{NpcId}:{Map}:{X:0.00}:{Y:0.00}");
    public bool CanMap => NpcId != 0 && Map != 0 && Territory != 0 && float.IsFinite(X) && float.IsFinite(Y) && X >= 1 && Y >= 1 && X < 100 && Y < 100;
}
public sealed record RouteRequirement(string Kind, uint Id, string Name);
public sealed record ShopRoute(uint ShopId, string Menu, NpcLocation Location, RouteRequirement[] Requirements);
public sealed record TradeCost(uint ItemId, string Name, uint Count, ushort Collectability = 0);
public sealed record QuestVariant(string Job, int Count, bool Hq, uint[]? Jobs = null);
public sealed record QuestUse(uint QuestId, string Name, bool Reward, bool Repeatable, NpcLocation[] Contacts, bool IssuerContact,
    int? RequiredCount = null, string Quality = "unknown", string Objective = "", int? TodoIndex = null,
    int? CompleteSequence = null, string Evidence = "", string Source = "", string[]? Variants = null, QuestVariant[]? RequirementVariants = null);
public sealed record DeliveryReward(uint ItemId, uint Count, bool Hq, byte Chance);
public sealed record DeliveryUse(uint Id, string Kind, string Name, int Count, string Quality, string Reward,
    NpcLocation[] Contacts, bool IssuerOnly = false, int Repeats = 0, ushort Collectability = 0, DeliveryReward[]? ItemRewards = null, string Requirements = "");
public sealed record RewardDetails(string Kind, string Name, string Description, uint PreviewIcon = 0, uint CollectionId = 0, string Detail = "");
public sealed record BuyOffer(string Shop, uint Price, bool Conditional, uint ShopId = 0, ShopRoute[]? Routes = null)
{
    public ShopRoute[] Destinations => Routes ?? [];
}
public sealed record ExchangeUse(string Shop, string Reward, uint Cost, uint ShopId = 0, uint RewardId = 0,
    uint RewardCount = 1, TradeCost[]? Costs = null, ShopRoute[]? Routes = null, RouteRequirement[]? Requirements = null)
{
    public TradeCost[] FullCosts => Costs ?? [];
    public ShopRoute[] Destinations => Routes ?? [];
    public RouteRequirement[] Conditions => Requirements ?? [];
    public string Key => $"{ShopId}:{RewardId}:{RewardCount}:" + string.Join(',', FullCosts.Select(c => $"{c.ItemId}:{c.Count}:{c.Collectability}"))
        + "|" + string.Join(',', Conditions.OrderBy(c => c.Kind).ThenBy(c => c.Id).Select(c => $"{c.Kind}:{c.Id}"));
}
public sealed record ItemSource(string Kind, string Name, uint Id = 0, string Detail = "");
public sealed record RecipeLink(uint RecipeId, uint ResultId, string Result, int Required, int Yield, string Job, int Level);
public sealed record QuestGate(uint Id, string Name, int Level, uint[] Jobs, RouteRequirement[] Previous, byte Join, string[] Additional, string JobLabel = "");
public sealed record ResearchCost(uint ItemId, string Name, int Count, bool Hq = false, int Collectability = 0);
public sealed record ResearchUse(string Kind, string Context, string WikiTitle = "", long Revision = 0,
    uint[]? Quests = null, string[]? Npcs = null, string Zone = "", ResearchCost[]? Costs = null,
    uint[]? Results = null, string[]? Requirements = null, int? RequiredCount = null, string Quality = "unknown", bool Repeatable = false, NpcLocation[]? Contacts = null, float? MapX = null, float? MapY = null, string Evidence = "");
public sealed record ItemFacts(uint Id, string Name, uint Icon, string Category, string Description,
    bool Equipment = false, bool Indisposable = false, bool Marketable = false, uint SellPrice = 0,
    bool Desynth = false, bool? Unlocked = null, BuyOffer[]? Offers = null, string[]? Recipes = null,
    ExchangeUse[]? Exchanges = null, QuestUse[]? Quests = null, NpcLocation[]? Merchants = null, RewardDetails? Reward = null, DeliveryUse[]? Deliveries = null, string Role = "", ItemSource[]? Sources = null, RecipeLink[]? RecipeLinks = null, ExchangeUse[]? Acquisitions = null, ResearchUse[]? Research = null)
{
    public ResearchUse[] ResearchUses => Research ?? [];
    public ItemSource[] AcquisitionSources => Sources ?? [];
    public RecipeLink[] CraftingLinks => RecipeLinks ?? [];
    public ExchangeUse[] AcquisitionTrades => Acquisitions ?? [];
    public DeliveryUse[] DeliveryUses => Deliveries ?? [];
    public BuyOffer[] BuyOffers => Offers ?? [];
    public string[] RecipeUses => Recipes ?? [];
    public ExchangeUse[] ExchangeUses => Exchanges ?? [];
    public QuestUse[] QuestUses => Quests ?? [];
    public NpcLocation[] SaleLocations => Merchants ?? [];
}

public sealed class ItemDecision
{
    public string Note { get; set; } = "";
    public bool AlwaysKeep { get; set; }
    public int KeepQuantity { get; set; }
    public DateTimeOffset? DeferUntil { get; set; }
}
public sealed class Goal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public bool Completed { get; set; }
    public Dictionary<uint, int> Items { get; set; } = new();
}
public sealed class PlanEntry
{
    public uint ItemId { get; set; }
    public PlanAction Action { get; set; }
    public int Quantity { get; set; }
    public int BaselineQuantity { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    // An inventory decrease cannot prove that a sale or hand-in happened.
    public bool ObservedDecrease { get; set; }
    public string DestinationKey { get; set; } = "";
    public string ExchangeKey { get; set; } = "";
    public string RouteKey { get; set; } = "";
}
public sealed class CharacterData
{
    public Dictionary<uint, ItemDecision> Decisions { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<PlanEntry> Plan { get; set; } = new();
    public ItemDecision Decision(uint id) => Decisions.GetValueOrDefault(id) ?? new();
    public ItemDecision Edit(uint id)
    {
        if (!Decisions.TryGetValue(id, out var value)) Decisions[id] = value = new();
        return value;
    }
}
public sealed record InventorySnapshot(ulong CharacterId, string CharacterName, DateTimeOffset CapturedAt,
    IReadOnlyList<InventoryStack> Stacks, IReadOnlyDictionary<uint, ItemFacts> Items,
    IReadOnlyDictionary<uint, string[]> Gearsets, bool GearsetsKnown, int BagCapacity,
    uint TerritoryId = 0, IReadOnlyDictionary<uint, bool>? CompletedQuests = null,
    IReadOnlyDictionary<uint, byte>? ActiveQuests = null, IReadOnlyDictionary<uint, bool?>? RewardUnlocks = null,
    IReadOnlyDictionary<uint, byte>? QuestJobs = null, IReadOnlyDictionary<uint, byte>? ActiveLeves = null, uint CurrentJob = 0, int CurrentLevel = 0)
{
    public int Quantity(uint id) => Stacks.Where(s => s.ItemId == id).Sum(s => s.Quantity);
    public int BagSlots => Stacks.Count(s => s.Container == ContainerKind.Bag);
    public int ArmourySlots => Stacks.Count(s => s.Container == ContainerKind.Armoury);
    public bool? QuestCompleted(uint id) => CompletedQuests != null && CompletedQuests.TryGetValue(id, out bool complete) ? complete : null;
}
public sealed record Advice(uint ItemId, AdviceKind Kind, Reason Reason, int Total, int Reserved, int Available,
    bool CanPlan, PlanAction DefaultAction, string[] GoalNames);
public sealed record PlanProjection(int Quantity, int BagSlots, int ArmourySlots, bool Blocked, bool Stale);

public static class CleanupPolicy
{
    public static int Allocated(InventorySnapshot snapshot, CharacterData data, Goal goal, uint itemId)
    {
        if (goal.Completed) return 0;
        int remaining = Math.Max(0, snapshot.Quantity(itemId) - QuestPolicy.Required(snapshot, itemId));
        foreach (var current in data.Goals.Where(g => !g.Completed))
        {
            int allocation = Math.Min(remaining, Math.Max(0, current.Items.GetValueOrDefault(itemId)));
            if (current.Id == goal.Id) return allocation;
            remaining -= allocation;
        }
        return 0;
    }

    public static int Reservation(CharacterData data, uint itemId)
    {
        long goals = data.Goals.Where(g => !g.Completed).Sum(g => (long)Math.Max(0, g.Items.GetValueOrDefault(itemId)));
        return (int)Math.Min(int.MaxValue, Math.Max(Math.Max(0, data.Decision(itemId).KeepQuantity), goals));
    }

    public static Advice Evaluate(InventorySnapshot snapshot, CharacterData data, uint id, DateTimeOffset now)
    {
        int total = snapshot.Quantity(id), reserved = QuestPolicy.Reservation(snapshot, data, id);
        var goals = data.Goals.Where(g => !g.Completed && g.Items.GetValueOrDefault(id) > 0).Select(g => g.Name).ToArray();
        var decision = data.Decision(id);
        Advice Result(AdviceKind kind, Reason reason, bool canPlan = false, PlanAction action = PlanAction.ManualReview) =>
            new(id, kind, reason, total, reserved, Math.Max(0, total - reserved), canPlan, action, goals);
        if (!snapshot.Items.TryGetValue(id, out var item)) return Result(AdviceKind.Protected, Reason.UnknownItem);
        if (decision.AlwaysKeep) return Result(AdviceKind.Protected, Reason.AlwaysKeep);
        if (snapshot.Stacks.Any(s => s.ItemId == id && s.Container == ContainerKind.Equipped)) return Result(AdviceKind.Protected, Reason.Equipped);
        if (snapshot.Gearsets.ContainsKey(id)) return Result(AdviceKind.Protected, Reason.Gearset);
        if (item.Equipment && !snapshot.GearsetsKnown) return Result(AdviceKind.Protected, Reason.GearsetsUnknown);
        if (item.Indisposable) return Result(AdviceKind.Protected, Reason.Indisposable);
        if (snapshot.Stacks.Any(s => s.ItemId == id && (s.Collectable || s.ValuableInstance))) return Result(AdviceKind.Protected, Reason.SpecialInstance);
        if (total <= reserved) return Result(AdviceKind.Reserved, QuestPolicy.Required(snapshot, id) > 0 ? Reason.QuestItem : Reason.FullReserve);
        if (decision.DeferUntil > now) return Result(AdviceKind.Deferred, Reason.Deferred);
        if (item.Unlocked == false) return Result(AdviceKind.Learn, Reason.Learn, true, PlanAction.Use);
        if (item.Unlocked == true) return Result(AdviceKind.Review, Reason.AlreadyLearned, true);
        if (item.Equipment) return Result(AdviceKind.Gear, Reason.GearReview, true,
            item.Desynth ? PlanAction.Desynthesize : PlanAction.ManualReview);
        if (item.ExchangeUses.Length > 0) return Result(AdviceKind.Exchange, Reason.ExchangeUses, true, PlanAction.ExchangeReview);
        if (item.QuestUses.Any(q => !q.Reward && (q.Repeatable || snapshot.QuestCompleted(q.QuestId) != true)))
            return Result(AdviceKind.Review, Reason.QuestItem, true);
        if (item.SellPrice > 0 && item.Description.Contains("Exchangeable for gil.", StringComparison.Ordinal))
            return Result(AdviceKind.Vendor, Reason.GilToken, true, PlanAction.Vendor);
        // A merchant listing is evidence of a reacquisition route, not a statement that the item is useless.
        if (item.BuyOffers.Any(o => !o.Conditional) && item.SellPrice > 0)
            return Result(AdviceKind.Review, Reason.VendorSource, true, PlanAction.ManualReview);
        if (reserved > 0) return Result(AdviceKind.Review, Reason.GoalReserve, true);
        return Result(AdviceKind.Review, item.RecipeUses.Length > 0 ? Reason.RecipeUses : Reason.UnknownUse, true);
    }

    public static bool Supports(ItemFacts item, PlanAction action) => action switch
    {
        PlanAction.Use => item.Unlocked == false,
        PlanAction.Vendor => item.SellPrice > 0 && !item.Indisposable,
        PlanAction.Desynthesize => item.Desynth,
        PlanAction.ExchangeReview => item.ExchangeUses.Length > 0,
        _ => true,
    };

    public static PlanProjection Project(InventorySnapshot snapshot, CharacterData data, PlanEntry entry, DateTimeOffset now)
    {
        var advice = Evaluate(snapshot, data, entry.ItemId, now);
        if (!advice.CanPlan || !snapshot.Items.TryGetValue(entry.ItemId, out var item) || !Supports(item, entry.Action))
            return new(0, 0, 0, true, entry.ObservedDecrease);
        int quantity = Math.Min(Math.Max(0, entry.Quantity), advice.Available);
        if (entry.Action == PlanAction.Use) quantity = Math.Min(1, quantity);
        bool stale = entry.ObservedDecrease || snapshot.Quantity(entry.ItemId) < entry.BaselineQuantity;
        if (stale) return new(quantity, 0, 0, false, true);
        // Review-only steps do not promise that an item will leave inventory.
        if (entry.Action is PlanAction.ManualReview or PlanAction.ExchangeReview) return new(quantity, 0, 0, false, false);
        var stacks = snapshot.Stacks.Where(s => s.ItemId == entry.ItemId && s.Container != ContainerKind.Equipped)
            .OrderBy(s => s.Quantity).ThenBy(s => s.Container).ThenBy(s => s.Slot);
        int remaining = quantity, bags = 0, armoury = 0;
        foreach (var stack in stacks)
        {
            if (stack.Quantity > remaining) break;
            remaining -= stack.Quantity;
            if (stack.Container == ContainerKind.Bag) bags++; else armoury++;
        }
        return new(quantity, bags, armoury, false, false);
    }

    public static void AddToPlan(InventorySnapshot snapshot, CharacterData data, Advice advice, PlanAction action, int quantity)
    {
        if (!advice.CanPlan || !snapshot.Items.TryGetValue(advice.ItemId, out var item) || !Supports(item, action)) return;
        quantity = Math.Clamp(quantity, 0, advice.Available);
        if (action == PlanAction.Use) quantity = Math.Min(1, quantity);
        if (quantity == 0) return;
        data.Plan.RemoveAll(p => p.ItemId == advice.ItemId);
        data.Plan.Add(new() { ItemId = advice.ItemId, Action = action, Quantity = quantity, BaselineQuantity = advice.Total });
    }
}

public static class DeliveryPolicy
{
    public static int EligibleQuantity(InventorySnapshot snapshot, uint itemId, DeliveryUse delivery) =>
        snapshot.Stacks.Where(s => s.ItemId == itemId && s.Container != ContainerKind.Equipped
            && (delivery.Quality == "collectable" || !s.Collectable)
            && (delivery.Quality != "hq" || s.Hq)
            && (delivery.Quality != "collectable" || s.Collectable && s.Collectability >= delivery.Collectability))
            .Sum(s => s.Quantity);
}

public sealed record AccessBlocker(string Kind, string Name = "", uint Id = 0, int Required = 0, int Actual = 0);
public static class QuestAccessPolicy
{
    public static AccessBlocker[] Check(InventorySnapshot snapshot, QuestGate gate)
    {
        if (snapshot.QuestCompleted(gate.Id) == true || snapshot.ActiveQuests?.ContainsKey(gate.Id) == true) return [];
        var result = new List<AccessBlocker>();
        if (snapshot.CurrentLevel == 0 || snapshot.CurrentJob == 0) result.Add(new("characterUnknown"));
        else
        {
            if (snapshot.CurrentLevel < gate.Level) result.Add(new("level", Required: gate.Level, Actual: snapshot.CurrentLevel));
            if (gate.Jobs.Length > 0 && !gate.Jobs.Contains(snapshot.CurrentJob)) result.Add(new("job"));
        }
        bool oneMet = gate.Previous.Any(p => snapshot.QuestCompleted(p.Id) == true);
        if (!(gate.Join == 2 && oneMet))
        {
            foreach (var previous in gate.Previous.Where(p => snapshot.QuestCompleted(p.Id) != true))
                result.Add(new(snapshot.QuestCompleted(previous.Id) == false ? (gate.Join == 2 ? "onePrevious" : "previous") : "previousUnknown", previous.Name, previous.Id));
        }
        result.AddRange(gate.Additional.Select(a => new AccessBlocker("additional", a)));
        return result.ToArray();
    }
}
