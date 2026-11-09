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
 static void Set(object instance,string name,object? value) => instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(instance,value);
 public static unsafe void Main(string[] args) {
  string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"XIVLauncher/addon/Hooks/dev");
  AssemblyLoadContext.Default.Resolving+=(_,n)=>File.Exists(Path.Combine(home,n.Name+".dll"))?AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(home,n.Name+".dll")):null;
  NativeLibrary.SetDllImportResolver(typeof(ImGui).Assembly,(n,_,_)=>NativeLibrary.Load(Path.Combine(home,n+".dll")));
  var plugin=(Plugin)RuntimeHelpers.GetUninitializedObject(typeof(Plugin));
  Set(plugin,"Search",new EliteSearch()); Set(plugin,"Session",new RouteSession()); Set(plugin,"CurrentTerritory",999u); Set(plugin,"HideDone",true); Set(plugin,"Ready",true); Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>()); Set(plugin,"BillStates",Enumerable.Range(0,22).Select(i=>(byte)i).ToDictionary(i=>i,i=>new BillState(true,i is 1 or 2 or 3 or 5,1,1,false)));
  var mobs=new List<MobHuntEntry>{new(){Name="Archaeornis",TerritoryName="Coerthas Western Highlands",BillNumber=1,MobIndex=0,MobHuntId=3481,Icon=84292,NeededKills=3,TerritoryType=397},new(){Name="Wooly Yak",TerritoryName="Coerthas Western Highlands",BillNumber=1,MobIndex=1,MobHuntId=3479,NeededKills=2,TerritoryType=397},new(){Name="Alteci",TerritoryName="Coerthas Western Highlands",BillNumber=5,MobIndex=0,MobHuntId=4350,NeededKills=1,IsEliteMark=true,TerritoryType=397}};
  Set(plugin,"Mobs",mobs); Set(plugin,"Routes",new List<ZoneRoute>{new("Coerthas Western Highlands",mobs,new(71,"Falcon's Nest",new(32,36))),new("The Sea of Clouds",mobs,new(73,"Camp Cloudtop",new(16,37))),new("The Dravanian Forelands",mobs,new(76,"Tailfeather",new(31,23)))});
  string directory=Path.GetFullPath(args[0]);Directory.CreateDirectory(directory);
  foreach(var sample in new[]{(520,430,17f,"normal"),(620,500,17f,"following"),(620,500,17f,"next"),(800,620,17f,"normal"),(1180,800,20f,"normal"),(520,430,17f,"completed"),(800,620,17f,"completed"),(520,430,20f,"completed-large"),(520,430,20f,"normal-large"),(520,430,17f,"settings"),(520,430,20f,"selected"),(620,500,17f,"elite"),(360,260,17f,"mini"),(360,360,20f,"mini-elite"),(520,430,20f,"settings-large"),(520,430,17f,"no-bills"),(520,430,17f,"deferred"),(520,430,17f,"unavailable"),(520,430,20f,"save-error"),(520,430,17f,"loading"),(400,340,17f,"normal"),(400,420,20f,"following"),(400,420,20f,"next"),(400,480,20f,"boards"),(620,620,17f,"boards-complete"),(400,480,20f,"targets"),(400,480,20f,"targets-selected"),(400,480,20f,"settings"),(300,280,20f,"mini-elite"),(400,480,20f,"paused"),(400,500,26f,"following"),(300,360,26f,"mini-elite"),(400,500,26f,"boards"),(400,480,20f,"boards-request"),(400,480,20f,"focus-request"),(400,480,20f,"settings-ordinary-only"),(400,480,20f,"settings-ordinary-off"),(400,560,20f,"settings-ranges"),(400,460,20f,"planned-travel"),(400,460,20f,"inactive-stale"),(400,460,20f,"stale-unavailable")}) {
   Set(plugin,"Mobs",mobs);Set(plugin,"SettingsOpen",sample.Item4.StartsWith("settings"));Set(plugin,"Selected",sample.Item4=="selected"?JourneyFlow.Key(mobs[0]):null!);
   var flow=(JourneyFlow)typeof(Plugin).GetProperty("Flow",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;flow.Stop();
   var search=new EliteSearch();Set(plugin,"Search",search);
   var session=new RouteSession();Set(plugin,"Session",session);Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>());
   var other=new MobHuntEntry{Name="Gaelicat",BillNumber=1,MobIndex=2,MobHuntId=3494,NeededKills=1,TerritoryType=401,TerritoryName="The Sea of Clouds"};
   bool here=sample.Item4 is "following" or "next" or "mini";
   bool completed=sample.Item4.StartsWith("completed")||sample.Item4=="boards-complete";
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
   if(here)flow.Active=true;
   if(completed) { Set(plugin,"Routes",new List<ZoneRoute>());Set(plugin,"Mobs",new List<MobHuntEntry>()); }
   Set(plugin,"Ready",sample.Item4!="loading");
   Set(plugin,"HistorySaveError",sample.Item4=="save-error"?"Не удалось сохранить отметки выполнения. В игре зачёт сохранён, но отметки могут пропасть после перезапуска.":null);
   flow.Unavailable.Clear();
   if(sample.Item4=="no-bills"){Set(plugin,"Mobs",new List<MobHuntEntry>());Set(plugin,"Routes",new List<ZoneRoute>());}
   if(sample.Item4=="deferred")foreach(var mob in mobs)flow.Deferred.Add(JourneyFlow.Key(mob));
   if(sample.Item4=="unavailable")foreach(var mob in mobs)flow.Unavailable[JourneyFlow.Key(mob)]="Нет проверенных координат";
   ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(sample.Item1,sample.Item2);io.DeltaTime=1f/60;
   io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",sample.Item3,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
   byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
   var window=new HuntWindow(plugin);
   var settings=(HuntSettings)typeof(Plugin).GetProperty("Settings",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;
   settings.WorldMarkers=sample.Item4!="settings-ordinary-only";settings.OrdinaryWorldMarkers=sample.Item4!="settings-ordinary-off";

   if(sample.Item4=="boards-request")Set(plugin,"PickupOpen",true);
   if(sample.Item4=="focus-request"){Set(plugin,"FocusRequested",true);Set(plugin,"Selected",JourneyFlow.Key(mobs[0]));}

   if(sample.Item4.StartsWith("boards"))Set(window,"page",HuntPage.Boards);
   if(sample.Item4.StartsWith("targets")||sample.Item4=="selected"){Set(window,"page",HuntPage.Targets);Set(window,"revealSelected",true);Set(plugin,"Selected",JourneyFlow.Key(mobs[0]));}
   if(sample.Item4=="paused"){session.Start(mobs[0],new byte[]{1,2,3});session.Tick(mobs,_=>false,_=>true,397);flow.Active=true;flow.Pauses=PauseReason.Marker|PauseReason.User;}

   if(sample.Item4=="planned-travel"){flow.Active=true;session.PlanTravel(other,new byte[]{1,2,3});}
   if(sample.Item4=="inactive-stale"){flow.Active=false;session.Retarget(mobs[0]);}
   if(sample.Item4=="stale-unavailable"){flow.Active=false;foreach(var mob in mobs)flow.Unavailable[JourneyFlow.Key(mob)]="Old unavailable cache";}
   string Journey()=>JsonSerializer.Serialize(new{session.RunId,session.Phase,Target=session.Target==null?null:JourneyFlow.Key(session.Target),flow.Active,flow.Pauses,flow.Manual,Deferred=flow.Deferred.Order().ToArray(),Zones=flow.Zones.Order().ToArray(),Kills=((Dictionary<(byte,byte),int>)typeof(Plugin).GetField("KillCounts",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!).OrderBy(p=>p.Key).Select(p=>$"{p.Key}:{p.Value}").ToArray(),Search=search.Capture(),search.ActorId});
   string before=Journey();float overflow=0;
   for(int frame=0;frame<5;frame++) {
    for(int page=0;page<io.Fonts.Textures.Size;page++)io.Fonts.SetTexID(page,new ImTextureID((ulong)page+1));
    ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);window.PreDraw();ImGui.Begin(sample.Item4=="offer"?"Продолжить охоту?":"Hunt Route",ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoMove|(sample.Item4.StartsWith("mini")?ImGuiWindowFlags.None:window.Flags));if(sample.Item4.StartsWith("mini"))typeof(HuntWindow).GetMethod("DrawSession",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{plugin});else {if(sample.Item4=="settings-ranges")ImGui.GetStateStorage().SetInt(ImGui.GetID("Дальность подсветки"),1);window.Draw();}overflow=Math.Max(overflow,ImGui.GetScrollMaxX());ImGui.End();window.PostDraw();ImGui.Render(); }
   string sampleName=$"huntroute-{sample.Item1}x{sample.Item2}-{sample.Item4}";
   var atlases=new Dictionary<ulong,string>();
   for(int page=0;page<io.Fonts.Textures.Size;page++){
    string atlas=$"{sampleName}-{page}.rgba";
    io.Fonts.GetTexDataAsRGBA32(page,&pixels,&tw,&th);
    File.WriteAllBytes(Path.Combine(directory,atlas),new ReadOnlySpan<byte>(pixels,tw*th*4).ToArray());
    atlases.Add((ulong)page+1,atlas);
   }
   var draw=ImGui.GetDrawData();var lists=new List<object>();
   for(int i=0;i<draw.CmdListsCount;i++) { var list=new ImDrawListPtr(draw.CmdLists[i]);var vertices=new List<double[]>();var indices=new List<ushort>();var commands=new List<object>();
    for(int n=0;n<list.VtxBuffer.Size;n++){var v=list.VtxBuffer[n];vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]);}
    for(int n=0;n<list.IdxBuffer.Size;n++)indices.Add(list.IdxBuffer[n]);
    for(int n=0;n<list.CmdBuffer.Size;n++){var c=list.CmdBuffer[n];commands.Add(new{Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W},Count=c.ElemCount,Offset=c.IdxOffset,VertexOffset=c.VtxOffset,Texture=c.TextureId.Handle});}
    lists.Add(new{Vertices=vertices,Indices=indices,Commands=commands});
   }
   File.WriteAllText(Path.Combine(directory,sampleName+".json"),JsonSerializer.Serialize(new{Width=sample.Item1,Height=sample.Item2,Font=sample.Item3,Atlases=atlases,TextureWidth=tw,TextureHeight=th,Lists=lists}));
   var actualPage=(HuntPage)typeof(HuntWindow).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
   if(sample.Item4=="boards-request"&&actualPage!=HuntPage.Boards)throw new Exception("Board selection request did not navigate to boards");
   if(sample.Item4=="focus-request"&&actualPage!=HuntPage.Targets)throw new Exception("Explicit focus did not reveal target list");
   if(before!=Journey())throw new Exception("Rendering mutated hunt state: "+sample);
   ImGui.DestroyContext();
   if(overflow>0)throw new Exception($"Horizontal overflow {overflow}px in {sample}");
   Console.WriteLine($"PASS production UI {sample}: exported vertices");
  }
  DialogPreviewChecks.Run(directory);
  WorldMarkerPreviewChecks.Run(directory);
  var nativeFlow=(JourneyFlow)typeof(Plugin).GetProperty("Flow",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;
  nativeFlow.Stop();nativeFlow.Unavailable.Clear();nativeFlow.Active=true;nativeFlow.Status="Сопровождение активно";

  var nativeSession=new RouteSession();nativeSession.Start(mobs[0],[1,2,3]);
  nativeSession.Tick(mobs,_=>false,_=>true,mobs[0].TerritoryType);
  Set(plugin,"Session",nativeSession);
  Set(plugin,"KillCounts",new Dictionary<(byte,byte),int>{{(1,0),1}});
  var boardHistory=new CompletionHistory();
  var boardStates=Enumerable.Range(1,21).ToDictionary(i=>(byte)i,i=>new BillState(true,i is 6 or 7 or 8,1,1,false));
  var sampleNow=DateTimeOffset.UtcNow;
  foreach(byte tier in new byte[]{1,2,3,5}){
   boardHistory.Observe(111,tier,tier==5,sampleNow,new(true,true,1,1,false));
   boardHistory.Observe(111,tier,tier==5,sampleNow,new(true,false,1,1,true));
   boardStates[tier]=new(true,false,1,1,true);
  }
  Set(plugin,"BillStates",boardStates);Set(plugin,"completionHistory",boardHistory);
  Set(plugin,"CurrentTerritory",418u); // An unlocked-by-location board can receive focus in the preview.
  NativeHuntPreviewChecks.Run(directory,plugin,args.Length>1?new Lumina.GameData(args[1]):null);
 }
}
