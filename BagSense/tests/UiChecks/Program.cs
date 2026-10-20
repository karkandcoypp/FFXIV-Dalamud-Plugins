using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using BagSense;
using Dalamud.Bindings.ImGui;
using Lumina;

internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Set(object instance, string property, object value) =>
        instance.GetType().GetField("<" + property + ">k__BackingField", Instance)!.SetValue(instance, value);
    public static unsafe void Main(string[] args)
    {
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher/addon/Hooks/dev");
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(home, name.Name + ".dll"))
            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home, name.Name + ".dll")) : null;
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly, (name, _, _) => NativeLibrary.Load(Path.Combine(home, name + ".dll")));
        string directory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(directory);
        foreach (var ru in new[] { true, false })
            foreach (int tab in new[] { 0, 1, 2 })
                foreach (var size in new[] { (1120, 760, 1f), (860, 570, 1f), (1440, 1000, 1.35f) })
                    Render(directory, ru, tab, size.Item1, size.Item2, size.Item3);
        if (args.Length > 1)
        {
            using var game = new GameData(args[1]);
            var catalog = new ItemCatalog(game.Excel, path => game.GetFile<Lumina.Data.Files.LgbFile>(path));
            if (args.Length > 2)
            {
                var manifest = JsonSerializer.Deserialize<CatalogManifest>(File.ReadAllText(Path.Combine(args[2], "manifest.json")))!;
                catalog.ApplyDatabase(DatabaseCatalog.Decode(manifest, File.ReadAllBytes(Path.Combine(args[2], manifest.File))));
            }
            var items = new uint[] { 47866, 41078, 23165, 5106, 5823 }.Select(id => catalog.Find(id)!).ToDictionary(i => i.Id,
                i => i with { Icon = 0, Merchants = catalog.World.Merchants.Where(m => m.Npc is "Bango Zango" or "Roarich" or "Maisenta").Take(5).ToArray() });
            var snapshot = new InventorySnapshot(ulong.MaxValue, "Sample character / Пример персонажа", DateTimeOffset.UtcNow,
                [new(47866, ContainerKind.Bag, 0, 1), new(41078, ContainerKind.Bag, 1, 18), new(23165, ContainerKind.Bag, 2, 26),
                    new(5106, ContainerKind.Bag, 3, 35), new(5823, ContainerKind.Bag, 4, 1)], items, new Dictionary<uint, string[]>(), true, 140,
                CompletedQuests: new Dictionary<uint, bool> { [70325] = false, [66135] = false }, ActiveQuests: new Dictionary<uint, byte> { [66135] = 255 });
            foreach (bool ru in new[] { true, false })
                foreach (uint id in items.Keys)
                    Render(directory, ru, 0, 1240, 860, 1, snapshot, id, catalog);
            Render(directory, true, 2, 1240, 860, 1, snapshot, 47866, catalog);
            foreach (bool ru in new[] { true, false })
                Render(directory, ru, 3, 1240, 860, 1, snapshot, 5106, catalog);
        }
    }

    private static unsafe void Render(string directory, bool ru, int tab, int width, int height, float scale, InventorySnapshot? actual = null, uint selected = 0, ItemCatalog? catalog = null)
    {
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        var settings = new Configuration { Russian = ru, Scale = scale };
        Set(plugin, "Settings", settings);
        Set(plugin, "Locale", new Text(() => settings.Russian));
        Set(plugin, "Demo", true);
        if (catalog != null) Set(plugin, "Catalog", catalog);
        var demo = DemoData.Create();
        if (actual != null)
        {
            demo = (actual, new CharacterData());
            foreach (var id in new uint[] { 47866, 41078, 5823 })
            {
                var advice = CleanupPolicy.Evaluate(actual, demo.Data, id, DateTimeOffset.UtcNow);
                CleanupPolicy.AddToPlan(actual, demo.Data, advice, advice.DefaultAction, Math.Max(1, advice.Available));
            }
        }
        typeof(Plugin).GetField("demoSnapshot", Instance)!.SetValue(plugin, demo.Snapshot);
        typeof(Plugin).GetField("demoData", Instance)!.SetValue(plugin, demo.Data);
        var window = new BagWindow(plugin, staticPreview: true);
        typeof(BagWindow).GetField("databaseItem", Instance)!.SetValue(window, selected);
        if (selected != 0) window.Select(selected);
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.IniFilename = null; io.DisplaySize = new(width, height); io.DeltaTime = 1f / 60;
        ushort* glyphs = stackalloc ushort[] { 0x20, 0xff, 0x400, 0x52f, 0x2000, 0x206f, 0 };
        io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf", 17f, (ImFontConfig*)null, glyphs);
        byte* pixels; int tw, th;
        io.Fonts.GetTexDataAsRGBA32(0, &pixels, &tw, &th);
        io.Fonts.SetTexID(0, new ImTextureID(1));
        for (int frame = 0; frame < 4; frame++)
        {
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(io.DisplaySize);
            window.PreDraw();
            ImGui.Begin("BagSense", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
            typeof(BagWindow).GetField("requestedTab", Instance)!.SetValue(window, tab);
            window.Draw();
            if (frame == 3 && ImGui.GetScrollMaxY() > 1) throw new Exception($"Outer window overflows: {ru}, {tab}, {width}, {scale}: {ImGui.GetScrollMaxY()}");
            ImGui.End(); window.PostDraw(); ImGui.Render();
        }
        io.Fonts.GetTexDataAsRGBA32(0, &pixels, &tw, &th);
        string name = $"bagsense-{(ru ? "ru" : "en")}-{tab}-{width}x{height}-{scale:0.00}" + (selected == 0 ? "" : "-item" + selected);
        File.WriteAllBytes(Path.Combine(directory, "font-" + name + ".rgba"), new ReadOnlySpan<byte>(pixels, tw * th * 4).ToArray());
        var draw = ImGui.GetDrawData();
        var lists = new List<object>();
        for (int i = 0; i < draw.CmdListsCount; i++)
        {
            var list = new ImDrawListPtr(draw.CmdLists[i]);
            var vertices = new List<double[]>(); var indices = new List<ushort>(); var commands = new List<object>();
            for (int n = 0; n < list.VtxBuffer.Size; n++) { var v = list.VtxBuffer[n]; vertices.Add([v.Pos.X, v.Pos.Y, v.Uv.X, v.Uv.Y, v.Col]); }
            for (int n = 0; n < list.IdxBuffer.Size; n++) indices.Add(list.IdxBuffer[n]);
            for (int n = 0; n < list.CmdBuffer.Size; n++) { var c = list.CmdBuffer[n]; commands.Add(new { Clip = new[] { c.ClipRect.X, c.ClipRect.Y, c.ClipRect.Z, c.ClipRect.W }, Count = c.ElemCount, Offset = c.IdxOffset, VertexOffset = c.VtxOffset }); }
            lists.Add(new { Vertices = vertices, Indices = indices, Commands = commands });
        }
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(new { Width = width, Height = height, Font = name, TextureWidth = tw, TextureHeight = th, Lists = lists }));
        Console.WriteLine($"PASS: production UI {name} ({draw.TotalVtxCount} vertices)");
        ImGui.DestroyContext();
    }
}
