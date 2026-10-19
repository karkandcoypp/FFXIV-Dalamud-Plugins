using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using EorzeaCompass;
using Lumina;
using Lumina.Excel;
using Lumina.Excel.Sheets;
internal static class Program
{
    static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object instance, string name, object value) => instance.GetType().GetField("<" + name + ">k__BackingField", Instance)!.SetValue(instance, value);
    public static unsafe void Main(string[] args)
    {
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher/addon/Hooks/dev");
        AssemblyLoadContext.Default.Resolving += (_, name) => File.Exists(Path.Combine(home, name.Name + ".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home, name.Name + ".dll")) : null;
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly, (name, _, _) => NativeLibrary.Load(Path.Combine(home, name + ".dll")));
        using var game = new GameData(args[0]);
        var catalog = new Catalog(game.Excel.GetSheet<Quest>(), game.Excel.GetSheet<ContentFinderCondition>());
        var plugin = (Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        Set(plugin, "Settings", new Configuration());
        Set(plugin, "Catalog", catalog);
        Set(plugin, "Available", true);
        Set(plugin, "CharacterId", 123ul);
        var observations = catalog.RelevantQuests.ToDictionary(id => id, _ => new QuestObservation(false,false,true,true));
        foreach (var story in catalog.Stories.Where(s=>s.Definition.Expansion < 2))
            foreach(var id in story.QuestIds) observations[id]=new(true,false,true,true);
        var ivalice=catalog.Stories.Single(s=>s.Definition.Name=="Return to Ivalice");
        var fresh=StoryCatalog.Evaluate(ivalice,catalog.Quests,observations);
        foreach(var id in fresh.Route.Take(4)) observations[id]=new(true,false,true,true);
        Set(plugin,"QuestProgress", observations);
        var stories = catalog.Stories.Select(s=>StoryCatalog.Evaluate(s,catalog.Quests,observations)).ToArray();
        Set(plugin,"Stories",stories);
        Set(plugin,"Expansions",StoryCatalog.Summarize(stories,catalog.Activities.Select(a=>new ActivityProgress(a,a.Expansion<2?ActivityState.Cleared:ActivityState.NotStarted,null,false))));
        var selectedNames = new[] { "Castrum Marinum", "the Cloud Deck", "Windurst: The Third Walk" };
        var rows = selectedNames.Select(name => catalog.Activities.First(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            .Select(a => new ActivityProgress(a, ActivityState.InProgress, new(a.QuestIds[0], false, true, false, "offer"), true)).ToArray();
        Set(plugin, "Progress", rows);
        var player = DispatchProxy.Create<IPlayerState, ServiceProxy>();
        ((ServiceProxy)(object)player).Handler = (method, _) => method.Name switch
        {
            "get_CharacterName" => "Carcosa Lyuman", "get_Level" => (short)100,
            "get_ClassJob" => game.Excel.GetSheet<Quest>().First(q => q.ClassJobRequired.RowId == 34).ClassJobRequired,
            _ => throw new NotSupportedException(method.Name),
        };
        typeof(Plugin).GetProperty("Player", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, player);
        var windowType = typeof(Plugin).Assembly.GetType("EorzeaCompass.CompassWindow")!;
        var window = Activator.CreateInstance(windowType, Instance, null, [plugin, new Func<uint, QuestLocation?>(_ => new("Landsguard messenger", "Tuliyollal", 0, 0, 16.2f,3.9f))], null)!;
        windowType.GetField("view", Instance)!.SetValue(window, 1);
        windowType.GetField("selected", Instance)!.SetValue(window, rows[2].Activity.Id);
        var directory = Path.GetFullPath(args[1]); Directory.CreateDirectory(directory);
        foreach (var sample in new[] { (1180,760,17f,"normal"), (960,600,17f,"normal"), (1440,900,22f,"normal"), (960,600,17f,"long"), (960,600,17f,"accepted"), (960,600,17f,"class"), (960,600,17f,"english"), (1180,760,17f,"overview"), (960,600,17f,"overview"), (1440,900,22f,"overview"), (1180,760,17f,"story"), (960,600,17f,"story"), (960,600,17f,"story-complete"), (960,600,17f,"story-unknown") })
        {
            var scenarioRow = rows[2];
            windowType.GetField("section",Instance)!.SetValue(window,sample.Item4=="overview"?0:sample.Item4.StartsWith("story")?1:2);
            var storyRow=stories.Single(s=>s.Story.Definition.Name=="Return to Ivalice");
            if(sample.Item4=="story-complete") storyRow=stories.Single(s=>s.Story.Definition.Name=="Crystal Tower");
            if(sample.Item4=="story-unknown") storyRow=storyRow with {State=StoryState.Unknown,Next=null};
            windowType.GetField("selectedStory",Instance)!.SetValue(window,storyRow.Story.Definition.Id);
            if (sample.Item4 == "long") scenarioRow = scenarioRow with { Activity = scenarioRow.Activity with { Name = "The Unending Coil of Bahamut - a deliberately long activity name to check wrapping" } };
            if (sample.Item4 == "accepted") scenarioRow = scenarioRow with { Next = scenarioRow.Next! with { Accepted = true, Reason = "accepted" } };
            if (sample.Item4 == "class") {
                scenarioRow = scenarioRow with { Next = scenarioRow.Next! with { Ready = false, Reason = "class" } };
                typeof(Plugin).GetField("jobHints",Instance)!.SetValue(plugin,new Dictionary<uint,string> { [scenarioRow.Next!.QuestId] = "SAM (100), PLD (100), WHM (100)" });
            }
            ((Configuration)typeof(Plugin).GetProperty("Settings",Instance)!.GetValue(plugin)!).Russian = sample.Item4 != "english";
            ImGui.CreateContext();
            var io = ImGui.GetIO(); io.IniFilename = null; io.DisplaySize = new(sample.Item1, sample.Item2); io.DeltaTime = 1f/60;
            io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf", 17f, (ImFontConfig*)null, io.Fonts.GetGlyphRangesCyrillic());
            io.FontGlobalScale = sample.Item3 / 17f;
            byte* pixels; int tw, th; io.Fonts.GetTexDataAsRGBA32(0, &pixels, &tw, &th);
            File.WriteAllBytes(Path.Combine(directory, $"font-{sample.Item3}.rgba"), new ReadOnlySpan<byte>(pixels, tw*th*4).ToArray());
            io.Fonts.SetTexID(0, new ImTextureID(1));
            for (var frame=0; frame<3; frame++)
            {
                ImGui.NewFrame();
                ImGui.SetNextWindowPos(Vector2.Zero); ImGui.SetNextWindowSize(io.DisplaySize);
                windowType.GetMethod("PreDraw")!.Invoke(window, null);
                ImGui.Begin("Eorzea Compass", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
                var scale = sample.Item3 / 17f;
                CompassTheme.Push(scale);
                ImGui.TextUnformatted("Твой путеводитель по Эорзее     Carcosa Lyuman / SAM · ур. 100");
                ImGui.Separator();
                ImGui.BeginChild("navigation",new Vector2(198*scale,0),true);
                windowType.GetMethod("DrawNavigation", Instance)!.Invoke(window,null);
                ImGui.EndChild(); ImGui.SameLine();
                ImGui.BeginChild("workspace",Vector2.Zero,false);
                if(sample.Item4=="overview") {
                    windowType.GetMethod("DrawOverview",Instance)!.Invoke(window,null);
                }
                else if(sample.Item4.StartsWith("story")) {
                    ImGui.TextUnformatted("Сюжетные цепочки"); ImGui.Separator();
                    ImGui.BeginChild("story-list",new Vector2(ImGui.GetContentRegionAvail().X*.44f,0),true);
                    foreach(var item in stories.Where(s=>s.Story.Definition.Expansion==storyRow.Story.Definition.Expansion))
                        windowType.GetMethod("DrawStoryRow",Instance)!.Invoke(window,[item]);
                    ImGui.EndChild();ImGui.SameLine();
                    ImGui.BeginChild("story-detail",Vector2.Zero,true);
                    windowType.GetMethod("DrawStoryDetail",Instance)!.Invoke(window,[storyRow]);
                    if(ImGui.GetContentRegionAvail().Y<0) throw new Exception("Story detail overflow");
                    ImGui.EndChild();
                }
                else {
                ImGui.TextUnformatted("Продолжить");
                ImGui.Button("Поиск по активности или квесту…",new Vector2(ImGui.GetContentRegionAvail().X*.52f,0));
                ImGui.SameLine(); ImGui.Button("Все дополнения",new Vector2(-1,0));
                bool check=false; ImGui.Checkbox("Можно начать сейчас",ref check); ImGui.SameLine(); ImGui.Button("Ещё фильтры");
                ImGui.TextUnformatted("Активностей: 3"); ImGui.Separator();
                ImGui.BeginChild("activity-list",new Vector2(Math.Clamp(ImGui.GetContentRegionAvail().X*.42f,240*scale,390*scale),0),true);
                foreach(var row in rows.Take(2).Append(scenarioRow)) {
                    var before=ImGui.GetCursorPosY();
                    windowType.GetMethod("DrawRow",Instance)!.Invoke(window,[row]);
                    if (sample.Item4 != "long" && ImGui.GetCursorPosY()-before > 90*scale) throw new Exception("Row too tall");
                }
                ImGui.EndChild(); ImGui.SameLine();
                ImGui.BeginChild("activity-detail",Vector2.Zero,true);
                windowType.GetMethod("DrawDetail",Instance)!.Invoke(window,[scenarioRow]);
                if (ImGui.GetContentRegionAvail().Y < 0) throw new Exception($"Details overflow {sample}");
                if(frame==2) Console.WriteLine($"PASS: geometry {sample}: remaining {ImGui.GetContentRegionAvail().Y}");
                ImGui.EndChild();
                }
                ImGui.EndChild();
                CompassTheme.Pop();
                ImGui.End(); ImGui.Render();
            }
            io.Fonts.GetTexDataAsRGBA32(0, &pixels, &tw, &th);
            File.WriteAllBytes(Path.Combine(directory, $"font-{sample.Item3}.rgba"), new ReadOnlySpan<byte>(pixels, tw*th*4).ToArray());
            var draw = ImGui.GetDrawData();
            var lists = new List<object>();
            for (int i=0;i<draw.CmdListsCount;i++)
            {
                var list = new ImDrawListPtr(draw.CmdLists[i]);
                var vertices = new List<double[]>(); var indices = new List<ushort>(); var commands = new List<object>();
                for(int n=0;n<list.VtxBuffer.Size;n++) { var v=list.VtxBuffer[n]; vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]); }
                for(int n=0;n<list.IdxBuffer.Size;n++) indices.Add(list.IdxBuffer[n]);
                for(int n=0;n<list.CmdBuffer.Size;n++) { var c=list.CmdBuffer[n]; commands.Add(new { Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W}, Count=c.ElemCount, Offset=c.IdxOffset, VertexOffset=c.VtxOffset }); }
                lists.Add(new { Vertices=vertices, Indices=indices, Commands=commands });
            }
            var name=$"compass-{sample.Item1}x{sample.Item2}-{sample.Item3}-{sample.Item4}";
            File.WriteAllText(Path.Combine(directory,name+".json"), JsonSerializer.Serialize(new { Width=sample.Item1, Height=sample.Item2, Font=sample.Item3, TextureWidth=tw,TextureHeight=th,Lists=lists }));
            Console.WriteLine($"PASS: rendered production panels {name}, {draw.TotalVtxCount} vertices");
            ImGui.DestroyContext();
        }
    }

}
public class ServiceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!,args);
}
