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
if (args.Length == 2 && args[0] == "--database") { DatabaseChecks.Run(args[1]); return; }
if (args.Length == 3 && args[0] == "--assets") { AssetChecks.Run(args[1], args[2]); return; }
if (args.Length == 2 && args[0] == "--online-database")
{
    var result = await BagSense.DatabaseCatalog.LoadAsync(args[1], CancellationToken.None);
    if (result.Offline || result.Document == null) throw new Exception("Public database download failed: " + result.Error);
    Console.WriteLine($"PASS: live production updater downloaded and cached {result.Document.Items.Length} items and {result.Document.Needs.Length} quest links");
    return;
}
if (args.Length == 2 && args[0] == "--data") { ConfigurationChecks.Run(); DataChecks.Run(args[1]); return; }
if (args.Length == 2 && args[0] == "--inspect") { RouteDiagnostics.Run(args[1]); return; }
if (args.Length > 1 && args[0] == "--schema")
{
    foreach (string name in args.Skip(1).SelectMany(n => n.EndsWith("*") ? typeof(Item).Assembly.GetTypes().Where(t => t.Namespace == "Lumina.Excel.Sheets" && t.Name.StartsWith(n[..^1])).Select(t => t.Name) : new[] { n }))
    {
        var type = typeof(Item).Assembly.GetType("Lumina.Excel.Sheets." + name) ?? typeof(Lumina.Excel.RowRef).Assembly.GetType("Lumina.Excel." + name);
        if (type == null) { Console.WriteLine("Missing " + name); continue; }
        Console.WriteLine("TYPE " + name);
        foreach (var property in type.GetProperties()) Console.WriteLine(" " + property.PropertyType.Name + " " + property.Name);
        foreach (var nested in type.GetNestedTypes())
        {
            Console.WriteLine(" NESTED " + nested.Name);
            foreach (var property in nested.GetProperties()) Console.WriteLine("  " + property.PropertyType.Name + " " + property.Name);
        }
    }
    return;
}
foreach (var t in new[] { typeof(GameInventoryType), typeof(GameInventoryItem), typeof(Item), typeof(GilShopItem), typeof(Recipe), typeof(MenuTargetInventory), typeof(RaptureGearsetModule), typeof(RaptureGearsetModule.GearsetEntry), typeof(RaptureGearsetModule.GearsetItem) })
{
    Console.WriteLine("TYPE " + t.FullName);
    if (t.IsEnum) Console.WriteLine(string.Join(", ", Enum.GetNames(t)));
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Console.WriteLine(" P " + p.PropertyType.Name + " " + p.Name);
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) Console.WriteLine(" F " + f.FieldType.Name + " " + f.Name);
    if (t == typeof(RaptureGearsetModule)) foreach (var m in t.GetMethods().Where(m => m.Name.Contains("Instance") || m.Name.Contains("Gearset"))) Console.WriteLine(" M " + m);
}
