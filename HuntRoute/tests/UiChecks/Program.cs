using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;
using Dalamud.Interface.Windowing;
class Program {
 static void Set(object instance,string name,object value) => instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(instance,value);
 public static unsafe void Main(string[] args) {
  string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
  AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(home,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home,n.Name+".dll")):null;
  NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly,(n,_,_)=>NativeLibrary.Load(Path.Combine(home,n+".dll")));
  var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
  Set(plugin,"Search",new EliteSearch()); Set(plugin,"Session",new RouteSession()); Set(plugin,"CurrentTerritory",999u); Set(plugin,"HideDone",true); Set(plugin,"Ready",true); Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>()); Set(plugin,"BillStates",Enumerable.Range(0,22).Select(i=>(byte)i).ToDictionary(i=>i,i=>new BillState(true,i is 1 or 2 or 3 or 5,1,1,false)));
  var mobs=new List<MobHuntEntry>{new(){Name="Archaeornis",TerritoryName="Coerthas Western Highlands",BillNumber=1,MobIndex=0,MobHuntId=3481,NeededKills=3,TerritoryType=397},new(){Name="Wooly Yak",TerritoryName="Coerthas Western Highlands",BillNumber=1,MobIndex=1,MobHuntId=3479,NeededKills=2,TerritoryType=397},new(){Name="Alteci",TerritoryName="Coerthas Western Highlands",BillNumber=5,MobIndex=0,MobHuntId=4350,NeededKills=1,IsEliteMark=true,TerritoryType=397}};
  Set(plugin,"Mobs",mobs); Set(plugin,"Routes",new List<ZoneRoute>{new("Coerthas Western Highlands",mobs,new(71,"Falcon's Nest",new(32,36))),new("The Sea of Clouds",mobs,new(73,"Camp Cloudtop",new(16,37))),new("The Dravanian Forelands",mobs,new(76,"Tailfeather",new(31,23)))});
  string directory=Path.GetFullPath(args[0]);Directory.CreateDirectory(directory);
  foreach(var sample in new[]{(520,430,17f,"normal"),(620,500,17f,"following"),(620,500,17f,"next"),(800,620,17f,"normal"),(1180,800,20f,"normal"),(520,430,17f,"completed"),(800,620,17f,"completed"),(520,430,20f,"completed-large"),(520,430,20f,"normal-large"),(520,430,17f,"settings"),(520,430,20f,"selected"),(620,500,17f,"elite"),(360,260,17f,"mini"),(360,360,20f,"mini-elite")}) {
   Set(plugin,"Mobs",mobs);Set(plugin,"SettingsOpen",sample.Item4=="settings");Set(plugin,"Selected",sample.Item4=="selected"?JourneyFlow.Key(mobs[0]):null!);
   var flow=(JourneyFlow)typeof(Plugin).GetProperty("Flow",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;flow.Stop();
   var search=new EliteSearch();Set(plugin,"Search",search);
   var session=new RouteSession();Set(plugin,"Session",session);Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>());
   var other=new MobHuntEntry{Name="Gaelicat",BillNumber=1,MobIndex=2,MobHuntId=3494,NeededKills=1,TerritoryType=401,TerritoryName="The Sea of Clouds"};
   bool here=sample.Item4 is "following" or "next" or "mini";
   bool completed=sample.Item4.StartsWith("completed");
   Set(plugin,"BillStates",Enumerable.Range(0,22).Select(i=>(byte)i).ToDictionary(i=>i,i=>new BillState(true,!completed && i is 1 or 2 or 3 or 5,1,1,completed && i is 1 or 2 or 3)));
   var history=new CompletionHistory();
   var snapshots=(Dictionary<byte,BillState>)typeof(Plugin).GetField("BillStates",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;
   foreach(var pair in snapshots) history.Observe(111,pair.Key,new byte[]{4,5,9,13,17,21}.Contains(pair.Key),DateTimeOffset.UtcNow,pair.Value);
   Set(plugin,"completionHistory",history);Set(plugin,"character",111UL);
   Set(plugin,"Routes",new List<ZoneRoute>{new("Coerthas Western Highlands",mobs,new(71,"Falcon's Nest",new(32,36)),here),new("The Sea of Clouds",new(){other},new(72,"Camp Cloudtop",new(10,33)))});
   if(here) {
    session.Start(mobs[0],new byte[]{1,2,3});
    if((sample.Item4 is "next" or "offer")) Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>{{(1,0),3},{(1,1),2}});
    session.Tick(mobs.Append(other).ToArray(),m=>(sample.Item4 is "next" or "offer")&&m.TerritoryType==397,_=>true,397);
   }
   if(sample.Item4 is "elite" or "mini-elite") {session.Start(mobs[2],new byte[]{5});session.Tick(mobs,_=>false,_=>true,397);flow.Active=true;search.Start([new(0,0)]);search.Observe(new(0,0),[new(1,new(10,10),false)],false,0,false,0,0);}
   if(sample.Item4=="mini")flow.Active=true;
   if(completed) { Set(plugin,"Routes",new List<ZoneRoute>());Set(plugin,"Mobs",new List<MobHuntEntry>()); }
   ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(sample.Item1,sample.Item2);io.DeltaTime=1f/60;
   io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",sample.Item3,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
   byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
   var window=new HuntWindow(plugin);
   float overflow=0;
   for(int frame=0;frame<3;frame++) { ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);window.PreDraw();ImGui.Begin(sample.Item4=="offer"?"Продолжить охоту?":"Hunt Route",ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoMove);if(sample.Item4.StartsWith("mini"))typeof(HuntWindow).GetMethod("DrawSession",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{plugin});else window.Draw();overflow=Math.Max(overflow,ImGui.GetScrollMaxX());ImGui.End();window.PostDraw();ImGui.Render(); }
   File.WriteAllBytes(Path.Combine(directory,$"font-{sample.Item3}.rgba"),new ReadOnlySpan<byte>(pixels,tw*th*4).ToArray());
   var draw=ImGui.GetDrawData();var lists=new List<object>();
   for(int i=0;i<draw.CmdListsCount;i++) { var list=new ImDrawListPtr(draw.CmdLists[i]);var vertices=new List<double[]>();var indices=new List<ushort>();var commands=new List<object>();
    for(int n=0;n<list.VtxBuffer.Size;n++){var v=list.VtxBuffer[n];vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]);}
    for(int n=0;n<list.IdxBuffer.Size;n++)indices.Add(list.IdxBuffer[n]);
    for(int n=0;n<list.CmdBuffer.Size;n++){var c=list.CmdBuffer[n];commands.Add(new{Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W},Count=c.ElemCount,Offset=c.IdxOffset,VertexOffset=c.VtxOffset});}
    lists.Add(new{Vertices=vertices,Indices=indices,Commands=commands});
   }
   File.WriteAllText(Path.Combine(directory,$"huntroute-{sample.Item1}x{sample.Item2}-{sample.Item4}.json"),JsonSerializer.Serialize(new{Width=sample.Item1,Height=sample.Item2,Font=sample.Item3,TextureWidth=tw,TextureHeight=th,Lists=lists}));
   ImGui.DestroyContext();
   if(overflow>0)throw new Exception($"Horizontal overflow {overflow}px in {sample}");
   Console.WriteLine($"PASS production UI {sample}: exported vertices");
  }
  DialogPreviewChecks.Run(directory);
 }
}
