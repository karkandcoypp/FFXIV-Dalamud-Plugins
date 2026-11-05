using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using VendorProfit;
using Dalamud.Bindings.ImGui;

internal static class Program
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static void Set(object instance,string name,object value) => instance.GetType().GetField(name,Instance)!.SetValue(instance,value);
    public static void Main(string[] args)
    {
        string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
        AssemblyLoadContext.Default.Resolving += (_,name)=>File.Exists(Path.Combine(home,name.Name+".dll")) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home,name.Name+".dll")) : null;
        NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly,(name,_,_)=>NativeLibrary.Load(Path.Combine(home,name+".dll")));
        string directory=Path.GetFullPath(args[0]);Directory.CreateDirectory(directory);
        foreach(int tab in new[]{0,1,2,3})
            foreach(var size in new[]{(980,650,1f),(850,520,1f),(1100,720,1.35f)})
                Render(directory,tab,size.Item1,size.Item2,size.Item3);
        Render(directory,1,850,520,1,"active");
        Render(directory,0,850,520,1,"empty");
        Render(directory,0,850,520,1,"error");
    }
    private static unsafe void Render(string directory,int tab,int width,int height,float scale,string scenario="normal")
    {
        // No plugin constructor, services, worker process or game calls. Only production draw methods.
        var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
        Set(plugin,"staticPreview",true);Set(plugin,"config",new Configuration{AutoBuyEnabled=scenario=="active"});Set(plugin,"world",80u);Set(plugin,"open",true);Set(plugin,"sized",true);
        Set(plugin,"externalStatus","Ready: World 80; offers 3; errors 0");Set(plugin,"itemFilter","");
        Set(plugin,"alertStatus","Поиск включён. Новые лоты появятся в вашем чате.");
        Set(plugin,"live",new List<Quote>());Set(plugin,"ledgerValid",true);
        var buyer=new AutoBuyer(new PreviewGateway(),_=>throw new Exception("Preview cannot persist purchases"));
        buyer.Attach(new BuyLedger{Character=123});Set(plugin,"autoBuyer",buyer);
        string[] names=["Clear Demimateria III","Craftsman's Command Materia II","Dinosaur Fossil"];
        var suggestions=names.Select((name,i)=>new Suggestion(new VendorItem((uint)(8144+i),name,5000,false,99,false),
            new Quote(new Lot((ulong)(i+1),(uint)(8144+i),"Sample retainer",4000,30,6000,false),126000,150000),
            DateTimeOffset.UtcNow,false,(uint)(80+i),i==0?"Cerberus":i==1?"Spriggan":"Omega")).ToArray();
        Set(plugin,"result",new ScanResult(80,DateTimeOffset.UtcNow,suggestions,100,0,0,"Preview"));
        if(scenario=="empty")Set(plugin,"result",new ScanResult(80,DateTimeOffset.UtcNow,[],100,70,0,"Preview"));
        if(scenario=="error")Set(plugin,"error","Пример ошибки соединения");
        ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(width,height);io.DeltaTime=1f/60;
        ushort* glyphs=stackalloc ushort[]{0x20,0xff,0x400,0x52f,0x2000,0x206f,0};
        io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",16*scale,(ImFontConfig*)null,glyphs);
        byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
        for(int frame=0;frame<4;frame++)
        {
            ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);
            Set(plugin,"requestedTab",tab);
            var colorBefore=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
            var paddingBefore=ImGui.GetStyle().FramePadding;
            typeof(Plugin).GetMethod("DrawWindow",Instance)!.Invoke(plugin,["Cerberus",false]);
            if(ImGui.GetStyle().Colors[(int)ImGuiCol.Text]!=colorBefore || ImGui.GetStyle().FramePadding!=paddingBefore)
                throw new Exception("Window theme leaked into other Dalamud windows");
            ImGui.Render();
        }
        io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);
        string name=$"vendorprofit-{tab}-{width}x{height}"+(scenario=="normal"?"":"-"+scenario);
        File.WriteAllBytes(Path.Combine(directory, "font-" + name + ".rgba"), new ReadOnlySpan<byte>(pixels, tw * th * 4).ToArray());
        var draw = ImGui.GetDrawData();
        var lists = new List<object>();
        for (int i = 0; i < draw.CmdListsCount; i++)
        {
            var list = new ImDrawListPtr(draw.CmdLists[i]);
            var vertices = new List<double[]>(); var indices = new List<ushort>(); var commands = new List<object>();
            for (int n = 0; n < list.VtxBuffer.Size; n++) { var v = list.VtxBuffer[n]; vertices.Add([v.Pos.X, v.Pos.Y, v.Uv.X, v.Uv.Y, v.Col]); }
            for (int n = 0; n < list.IdxBuffer.Size; n++) indices.Add(list.IdxBuffer[n]);
            for (int n = 0; n < list.CmdBuffer.Size; n++) { var c = list.CmdBuffer[n]; commands.Add(new { Texture = c.TextureId.Handle, Clip = new[] { c.ClipRect.X, c.ClipRect.Y, c.ClipRect.Z, c.ClipRect.W }, Count = c.ElemCount, Offset = c.IdxOffset, VertexOffset = c.VtxOffset }); }
            lists.Add(new { Vertices = vertices, Indices = indices, Commands = commands });
        }
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(new { Width = width, Height = height, Font = name, TextureWidth = tw, TextureHeight = th, Lists = lists,
            Textures = Array.Empty<object>() }));
        Console.WriteLine($"PASS: production UI {name} ({draw.TotalVtxCount} vertices)");
        ImGui.DestroyContext();
    }
}
internal sealed class PreviewGateway : IBuyGateway
{
    public bool Request(uint item)=>throw new Exception("Preview cannot query game");
    public Lot[] FreshLots(uint item)=>[];
    public bool Submit(Lot lot)=>throw new Exception("Preview cannot buy");
    public int ItemCount(uint item,bool hq)=>0;
    public void Clear(){}
}
