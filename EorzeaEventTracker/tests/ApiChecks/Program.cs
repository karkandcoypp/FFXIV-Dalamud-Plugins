using System;
using System.Linq;
using Dalamud.Game.Inventory;

if (args.Length == 3 && args[0] == "--installed-data")
{
    var dev = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
    System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
    {
        var path = System.IO.Path.Combine(dev, name.Name + ".dll");
        return System.IO.File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
    };
    InstalledDataChecks.Run(args[1], args[2]);
    return;
}
var names = Enum.GetNames<GameInventoryType>();
foreach (var required in new[] { "Inventory1", "Inventory2", "Inventory3", "Inventory4", "EquippedItems" })
    if (!names.Contains(required)) throw new Exception("Missing inventory container: " + required);
var armory = names.Where(n => n.StartsWith("Armory", StringComparison.Ordinal)).ToArray();
if (armory.Length == 0) throw new Exception("No armory containers found");
Console.WriteLine("PASS: bags, equipped items and " + armory.Length + " armory container names verified against installed Dalamud");
