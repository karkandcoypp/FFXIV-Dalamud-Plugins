namespace BagSense;

public sealed record BundlePart(uint ItemId, string Name, int? Count, string Quality = "any", int Collectability = 0, int? Objective = null, bool Scanned = true);
public sealed record ItemBundle(string Key, string Name, string Kind, BundlePart[] Parts, uint QuestId = 0,
    NpcLocation[]? Contacts = null, bool Exact = true, string Wiki = "", uint ResultId = 0);
public sealed record BundleProgress(BundlePart Part, int Owned, int? Eligible, int? Missing, int Equipped);

// Read-only projection: bundles never allocate stock or weaken cleanup protections.
public static class BundlePolicy
{
    public static BundleProgress Progress(InventorySnapshot snapshot, BundlePart part)
    {
        var stacks = snapshot.Stacks.Where(s => s.ItemId == part.ItemId).ToArray();
        int owned = stacks.Sum(s => s.Quantity);
        // Currencies and key-item storage are not covered by the bag scan.
        bool readable = part.Scanned && part.ItemId >= 100 && part.ItemId < 2000000 && part.Quality is "any" or "hq";
        int? eligible = readable ? stacks.Where(s => !s.Collectable && part.Collectability == 0 && (part.Quality != "hq" || s.Hq)
            || part.Collectability > 0 && s.Collectable && s.Collectability >= part.Collectability && (part.Quality != "hq" || s.Hq)).Sum(s => s.Quantity) : null;
        return new(part, owned, eligible, part.Count is > 0 && eligible.HasValue ? Math.Max(0, part.Count.Value - eligible.Value) : null,
            stacks.Where(s => s.Container == ContainerKind.Equipped).Sum(s => s.Quantity));
    }

    public static BundleProgress[] Evaluate(ItemBundle bundle, InventorySnapshot snapshot)
    {
        var remaining = snapshot.Stacks.ToArray();
        var rows = new BundleProgress[bundle.Parts.Length];
        // Allocate stricter copies first. HQ or high-collectability copies cannot satisfy two prices at once.
        foreach (int index in Enumerable.Range(0, rows.Length).OrderByDescending(i => bundle.Parts[i].Quality == "hq")
            .ThenByDescending(i => bundle.Parts[i].Collectability))
        {
            var part = bundle.Parts[index];
            rows[index] = Progress(snapshot with { Stacks = remaining }, part) with { Owned = snapshot.Quantity(part.ItemId) };
            int toUse = part.Count is > 0 ? Math.Min(part.Count.Value, rows[index].Eligible ?? 0) : 0;
            for (int i = 0; i < remaining.Length && toUse > 0; i++)
            {
                var stack = remaining[i];
                if (stack.ItemId != part.ItemId || part.Quality == "hq" && !stack.Hq
                    || (part.Collectability > 0 ? !stack.Collectable || stack.Collectability < part.Collectability : stack.Collectable)) continue;
                int take = Math.Min(toUse, stack.Quantity);
                remaining[i] = stack with { Quantity = stack.Quantity - take }; toUse -= take;
            }
        }
        return rows;
    }

    public static bool Collected(ItemBundle bundle, InventorySnapshot snapshot) => bundle.Exact && bundle.Parts.Length > 0
        && Evaluate(bundle, snapshot).All(p => p.Missing == 0);

    public static BundlePart? QuestPart(uint id, string name, QuestUse use, InventorySnapshot snapshot, bool scanned = true)
    {
        if (use.RequirementVariants is { Length: > 0 } variants)
        {
            if (snapshot.QuestJobs?.TryGetValue(use.QuestId, out byte job) != true || job == 0)
                return new(id, name, null, "unknown", Objective: use.TodoIndex, Scanned: scanned);
            var applicable = variants.Where(v => v.Jobs is not { Length: > 0 } || v.Jobs.Contains(job)).DistinctBy(v => (v.Count, v.Hq)).ToArray();
            if (applicable.Length == 0) return null;
            if (applicable.Length > 1) return new(id, name, null, "unknown", Objective: use.TodoIndex, Scanned: scanned);
            return new(id, name, applicable[0].Count, applicable[0].Hq ? "hq" : "any", Objective: use.TodoIndex, Scanned: scanned);
        }
        return new(id, name, use.RequiredCount, use.Quality, Objective: use.TodoIndex, Scanned: scanned);
    }
}

public sealed record DemiatmaGuide(uint Id, string Name, string Zone, string Region, string[] Events)
{
    public static readonly DemiatmaGuide[] All = [
        new(47744, "Azurite Demiatma", "Urqopacha", "southeast", ["A Prying Eye", "Rough Waters", "The Golden Guardian", "Crawling Death", "Scourge of the Mind", "The Unbridled"]),
        new(47745, "Verdigris Demiatma", "Kozama'uka", "southwest", ["A Delicate Balance", "Sworn to Soil", "Pleading Pots", "Calamity Bound"]),
        new(47746, "Malachite Demiatma", "Yak T'el", "west", ["An Unending Duty", "Flame of Dusk", "Trial by Claw", "From Times Bygone"]),
        new(47747, "Realgar Demiatma", "Shaaloani", "north", ["Brain Drain", "The Winged Terror", "With Extreme Prejudice", "Shark Attack", "Cursed Concern"]),
        new(47748, "Caput Mortuum Demiatma", "Heritage Found", "east", ["Serving Darkness", "Company of Stone", "Eternal Watch", "On the Hunt"]),
        new(47749, "Orpiment Demiatma", "Living Memory", "center / northeast", ["Fatal Allure", "King of the Crescent", "Persistent Pots", "Noise Complaint", "The Black Regiment"])
    ];
    public static DemiatmaGuide? Find(uint id) => All.FirstOrDefault(g => g.Id == id);
}
