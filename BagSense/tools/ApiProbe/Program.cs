using System.Reflection;
using Dalamud.Game.Inventory;
using Dalamud.Game.Gui.ContextMenu;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

var dev = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks", "dev");
System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var path = Path.Combine(dev, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
if (args.Length == 1 && args[0] == "--configuration") { ConfigurationChecks.Run(); return; }
if (args.Length == 2 && args[0] == "--data") { ConfigurationChecks.Run(); DataChecks.Run(args[1]); return; }
foreach (var t in new[] { typeof(GameInventoryType), typeof(GameInventoryItem), typeof(Item), typeof(GilShopItem), typeof(Recipe), typeof(MenuTargetInventory), typeof(RaptureGearsetModule), typeof(RaptureGearsetModule.GearsetEntry), typeof(RaptureGearsetModule.GearsetItem) })
{
    Console.WriteLine("TYPE " + t.FullName);
    if (t.IsEnum) Console.WriteLine(string.Join(", ", Enum.GetNames(t)));
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Console.WriteLine(" P " + p.PropertyType.Name + " " + p.Name);
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Console.WriteLine(" F " + f.FieldType.Name + " " + f.Name);
    if (t == typeof(RaptureGearsetModule)) foreach (var m in t.GetMethods().Where(m => m.Name.Contains("Instance") || m.Name.Contains("Gearset"))) Console.WriteLine(" M " + m);
}
