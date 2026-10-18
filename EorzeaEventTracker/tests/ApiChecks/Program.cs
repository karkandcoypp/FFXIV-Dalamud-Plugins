using System;
using System.Linq;
using Dalamud.Game.Inventory;

var names = Enum.GetNames<GameInventoryType>();
foreach (var required in new[] { "Inventory1", "Inventory2", "Inventory3", "Inventory4", "EquippedItems" })
    if (!names.Contains(required)) throw new Exception("Missing inventory container: " + required);
var armory = names.Where(n => n.StartsWith("Armory", StringComparison.Ordinal)).ToArray();
if (armory.Length == 0) throw new Exception("No armory containers found");
Console.WriteLine("PASS: bags, equipped items and " + armory.Length + " armory container names verified against installed Dalamud");
