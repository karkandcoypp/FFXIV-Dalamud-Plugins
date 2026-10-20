using System.Text.Json;
using BagSense;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
var now = DateTimeOffset.UtcNow;
var (snapshot, data) = DemoData.Create();
Advice Advice(uint id) => CleanupPolicy.Evaluate(snapshot, data, id, now);
PlanProjection Project(PlanAction action, int quantity, uint id = 900001, int? baseline = null) =>
    CleanupPolicy.Project(snapshot, data, new() { ItemId = id, Action = action, Quantity = quantity, BaselineQuantity = baseline ?? snapshot.Quantity(id) }, now);

Check(Advice(900001).Reserved == 20 && Advice(900001).Available == 15, "Goal reserves 20 of 35");
Check(Project(PlanAction.Vendor, 15).BagSlots == 0, "A partial 35-unit stack frees no slots");
Check(Project(PlanAction.Vendor, 35).Quantity == 15, "Plan cannot consume goal reservation");
data.Goals.Add(new() { Name = "Second project", Items = new() { [900001] = 10 } });
Check(Advice(900001).Reserved == 30 && Advice(900001).Available == 5, "Multiple goals add reservations");
data.Edit(900001).KeepQuantity = 40;
Check(Advice(900001).Kind == AdviceKind.Reserved, "Personal stock can exceed goal needs");
data.Edit(900001).KeepQuantity = 25;
Check(Advice(900001).Reserved == 30, "Personal stock is a minimum, not a second allocation");
data.Goals[0].Completed = true;
Check(Advice(900001).Reserved == 25, "Completed goals release reservations");
data.Goals[0].Completed = false;
data.Edit(900001).KeepQuantity = 0;
Check(CleanupPolicy.Allocated(snapshot, data, data.Goals[0], 900001) == 20, "First goal gets its 20");
Check(CleanupPolicy.Allocated(snapshot, data, data.Goals[1], 900001) == 10, "Second goal gets 10 remaining units");
data.Goals[1].Items[900001] = 30;
Check(CleanupPolicy.Allocated(snapshot, data, data.Goals[1], 900001) == 15, "Allocation never double counts held units");
data.Goals.RemoveAt(1);

Check(Advice(900005).Reason == Reason.Gearset && !Advice(900005).CanPlan, "Saved gearsets protect equipment");
Check(Advice(900008).Reason == Reason.SpecialInstance && !Advice(900008).CanPlan, "Melded equipment protected");
Check(Advice(900007).Kind == AdviceKind.Reserved, "Missing goal quantities stay reserved");
Check(Advice(900003).DefaultAction == PlanAction.Use, "Unlearned collectibles suggested");
Check(Project(PlanAction.Use, 10, 900003).Quantity == 1, "Only one unlock item is planned");
Check(Project(PlanAction.Vendor, 15, 900002).BagSlots == 2, "Removing both complete stacks frees two slots");
Check(Project(PlanAction.Vendor, 3, 900002).BagSlots == 1, "Smallest stack removed first");
Check(Project(PlanAction.Vendor, 2, 900002).BagSlots == 0, "Partial smallest stack frees no slots");
Check(Project(PlanAction.ExchangeReview, 7, 900006).BagSlots == 0, "Exchange review does not promise released slots");
Check(Project(PlanAction.ManualReview, 15, 900002).BagSlots == 0, "Manual review does not promise released slots");
data.Edit(900002).AlwaysKeep = true;
Check(Project(PlanAction.Vendor, 15, 900002).Blocked, "Protection blocks an existing plan");
data.Edit(900002).AlwaysKeep = false;
data.Edit(900002).DeferUntil = now.AddDays(1);
Check(Advice(900002).Kind == AdviceKind.Deferred && !Advice(900002).CanPlan, "Deferred items wait");
data.Edit(900002).DeferUntil = now.AddSeconds(-1);
Check(Advice(900002).CanPlan, "Deferred decisions return after expiry");
data.Edit(900002).DeferUntil = null;

var original = snapshot;
snapshot = snapshot with { GearsetsKnown = false, Gearsets = new Dictionary<uint, string[]>() };
Check(!Advice(900004).CanPlan && Advice(900004).Reason == Reason.GearsetsUnknown, "Unknown gearsets protect all equipment");
snapshot = original with { Stacks = original.Stacks.Append(new(900004, ContainerKind.Equipped, 0, 1)).ToArray() };
Check(Advice(900004).Reason == Reason.Equipped, "Equipped type and duplicate copies protected");
snapshot = original with { Items = original.Items.Where(p => p.Key != 900002).ToDictionary() };
Check(Advice(900002).Reason == Reason.UnknownItem && !Advice(900002).CanPlan, "Unknown items cannot be cleaned up");
snapshot = original with { Items = original.Items.ToDictionary(p => p.Key, p => p.Key == 900002 ? p.Value with { Indisposable = true } : p.Value) };
Check(Advice(900002).Reason == Reason.Indisposable, "Non-disposable items protected");
snapshot = original with { Stacks = original.Stacks.Select(s => s.ItemId == 900002 ? s with { Collectable = true } : s).ToArray() };
Check(Advice(900002).Reason == Reason.SpecialInstance, "Collectability cannot be treated as a regular stack");
snapshot = original;

CleanupPolicy.AddToPlan(snapshot, data, Advice(900001), PlanAction.Vendor, 999);
Check(data.Plan.Single(p => p.ItemId == 900001).Quantity == 15, "Add clamps quantity to surplus");
CleanupPolicy.AddToPlan(snapshot, data, Advice(900001), PlanAction.Vendor, 5);
Check(data.Plan.Count(p => p.ItemId == 900001) == 1 && data.Plan.Single(p => p.ItemId == 900001).Quantity == 5, "One step per type prevents double counting");
Check(Project(PlanAction.Use, 1, 900002).Blocked, "Unsupported action is blocked");
Check(Project(PlanAction.Vendor, 15, 900002, 20).Stale, "Decrease since baseline requires review");
Check(Project(PlanAction.Vendor, 15, 900002, 20).BagSlots == 0, "Stale plan cannot promise freed slots");
var sticky = new PlanEntry { ItemId = 900002, Action = PlanAction.Vendor, Quantity = 15, BaselineQuantity = 15, ObservedDecrease = true };
Check(CleanupPolicy.Project(snapshot, data, sticky, now).Stale, "A re-acquired item does not silently reactivate a stale plan");
data.Edit(900002).KeepQuantity = 14;
Check(Project(PlanAction.Vendor, 15, 900002).Quantity == 1 && Project(PlanAction.Vendor, 15, 900002).BagSlots == 0, "New reservation immediately constrains existing plan");
data.Edit(900002).KeepQuantity = 0;

var persisted = JsonSerializer.Deserialize<CharacterData>(JsonSerializer.Serialize(data))!;
Check(persisted.Goals[0].Items[900001] == 20, "Goal quantities survive restart");
Check(persisted.Decision(900001).Note == "For ingots / Для слитков", "Unicode item note survives restart");
Check(persisted.Plan.Count == data.Plan.Count, "Plan survives restart");
var otherCharacter = new CharacterData();
Check(otherCharacter.Goals.Count == 0 && otherCharacter.Decisions.Count == 0, "New character has independent data");
var demo2 = DemoData.Create();
Check(demo2.Data.Plan.All(p => p.ItemId != 900001), "Demo reset creates independent data");

snapshot = original with { Stacks = [new(900002, ContainerKind.Bag, 0, 5, Hq: true), new(900002, ContainerKind.Armoury, 0, 2)] };
Check(Project(PlanAction.Vendor, 2, 900002).ArmourySlots == 1 && Project(PlanAction.Vendor, 2, 900002).BagSlots == 0, "Bags and Armoury slots stay separate");
Check(Project(PlanAction.Vendor, 7, 900002).BagSlots == 1 && Project(PlanAction.Vendor, 7, 900002).ArmourySlots == 1, "HQ and containers do not merge physical slots");
data.Edit(900002).KeepQuantity = 6;
Check(Project(PlanAction.Vendor, 7, 900002).Quantity == 1, "HQ and NQ share base-item reservation");
data.Goals.Add(new() { Items = new() { [900002] = int.MaxValue } });
data.Goals.Add(new() { Items = new() { [900002] = int.MaxValue } });
Check(CleanupPolicy.Reservation(data, 900002) == int.MaxValue, "Large goal totals saturate without integer overflow");
var ru = new Text(() => true); var en = new Text(() => false);
foreach (var reason in Enum.GetValues<Reason>()) Check(ru.Reason(reason).Length > 0 && en.Reason(reason).Length > 0 && ru.Reason(reason) != en.Reason(reason), "Both languages cover " + reason);
foreach (var kind in Enum.GetValues<AdviceKind>()) Check(ru.Kind(kind) != en.Kind(kind), "Both languages cover " + kind);
foreach (var act in Enum.GetValues<PlanAction>()) Check(ru.Action(act) != en.Action(act), "Both languages cover " + act);
Console.WriteLine($"PASS: {checks} policy, persistence, isolation and localization checks");
