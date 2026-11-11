using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;

// Offline check of actual native layout data; the game's Atk renderer/focus are not invoked.
internal static class NativeHuntPreviewChecks {
 internal static unsafe void Run(string directory,Plugin plugin,Lumina.GameData? game){
  var controller=new NativeHuntController(plugin);
  var samples=new List<(string Name,NativeHuntPage Page,NativeHuntScreen Screen,bool Mini,int Offset)>();
  foreach(var page in Enum.GetValues<NativeHuntPage>()){if(page==NativeHuntPage.Target)controller.FocusGoal();else controller.Navigate(page);samples.Add((page.ToString(),page,controller.Build(),false,0));}
  var boards=samples.Single(s=>s.Page==NativeHuntPage.Boards);
  var longest=TimeSpan.FromMinutes(23*60+59);
  samples.Add(("Boards-max-timers",boards.Page,boards.Screen with {BoardTimers=NativeBoardTimers.FromRemaining(longest,longest)},false,0));
  var number=samples.Single(s=>s.Page==NativeHuntPage.Number);
  samples.Add(("Number-lower",number.Page,number.Screen,false,0));
  samples.Add(("mini",NativeHuntPage.Hunt,controller.Build(true),true,0));
  var flow=(JourneyFlow)typeof(Plugin).GetProperty("Flow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(plugin)!;
  var priorPauses=flow.Pauses;
  try{
   flow.Pauses=PauseReason.Marker;
   var paused=controller.Build(true);
   if(paused.Summary!="Метка изменена")throw new Exception("Mini panel lost the pause reason");
   samples.Add(("mini-paused",NativeHuntPage.Hunt,paused,true,0));
  }finally{flow.Pauses=priorPauses;}
  var session=(RouteSession)typeof(Plugin).GetField("Session",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(plugin)!;
  var current=session.Target??throw new Exception("Expected active hunt target");
  var mobField=typeof(Plugin).GetField("Mobs",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
  var mobs=mobField.GetValue(plugin);
  try{
   flow.Active=false;mobField.SetValue(plugin,new List<MobHuntEntry>());
   controller.Navigate(NativeHuntPage.Hunt);
   samples.Add(("Hunt-empty",controller.Page,controller.Build(),false,0));
   controller.FocusGoal();
   samples.Add(("Target-missing",controller.Page,controller.Build(),false,0));
  }finally{flow.Active=true;mobField.SetValue(plugin,mobs);}
  controller.Navigate(NativeHuntPage.Actions);
  session.PlanTravel(current,[current.BillNumber]);
  samples.Add(("Actions-planned",NativeHuntPage.Actions,controller.Build(),false,0));
  session.RequestNext(current);
  samples.Add(("Actions-next",NativeHuntPage.Actions,controller.Build(),false,0));
  session.Retarget(current);
  controller.Navigate(NativeHuntPage.Hunt);
  var notice=typeof(Plugin).GetProperty("NoticeText",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)
   ?? throw new Exception("Missing notice field");
  var oldNotice=notice.GetValue(plugin);
  try{
   notice.SetValue(plugin,null);
   var clean=controller.Build();
   notice.SetValue(plugin,"Доступна следующая зона.");
   var arrived=controller.Build();
   if(arrived.Summary!=clean.Summary||!arrived.Buttons.SequenceEqual(clean.Buttons))
    throw new Exception("The old zone notice must not change the current target summary or layout after arrival");
   controller.Navigate(NativeHuntPage.Actions);
   controller.Execute(controller.Build().Buttons.Single(b=>b.Key=="message").Action);
   if(controller.Build().Summary!="Доступна следующая зона.")
    throw new Exception("The route notice must be readable from Route actions");
   var manes=new MobHuntEntry{Name="Manes",IsEliteMark=true,BillNumber=19,NeededKills=1};
   var counts=(Dictionary<(byte,byte),int>)typeof(Plugin).GetField("KillCounts",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(plugin)!;
   var ping=typeof(Plugin).GetMethod("Ping",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
   try{
    session.Retarget(manes);
    ping.Invoke(plugin,["Элита обнаружена: Manes",false,manes]);
    controller.Navigate(NativeHuntPage.Actions);
    controller.Execute(controller.Build().Buttons.Single(b=>b.Key=="message").Action);
    if(!controller.Build().Summary.Contains("Manes"))throw new Exception("Current elite notice missing");
    counts[(19,0)]=1;
    if(controller.Build().Summary.Contains("Manes"))throw new Exception("Credited elite notice is still visible");
    counts.Remove((19,0));
    session.PlanTravel(current,[current.BillNumber]);
    if(controller.Build().Summary.Contains("Manes"))throw new Exception("Old elite notice followed a new contract");
    controller.Navigate(NativeHuntPage.Hunt);
    if(controller.Build().Summary.Contains("Manes"))throw new Exception("Old elite notice is visible under a new target");
    flow.Active=false;
    var stopped=controller.Build();
    if(stopped.Elite||stopped.Progress!=null||stopped.Icon!=0)throw new Exception("Stopped route shows old target data");
   }finally{counts.Remove((19,0));flow.Active=true;session.Retarget(current);}
   Console.WriteLine("PASS credited elite notice expires before the next contract");
   Console.WriteLine("PASS after-arrival summary stays compact and route notice remains accessible");
  }finally{notice.SetValue(plugin,oldNotice);}

  var longButtons=Enumerable.Range(0,13).Select(i=>new NativeHuntButton("goal:"+i,$"1/3 · {new string('Ж',100)}",new("target",i.ToString()),i!=6)).ToArray();
  samples.Add(("long-paged",NativeHuntPage.Targets,new(new string('Ж',100),"Текущая цель · длинная подпись\nСопровождение сохранено. Маршрут продолжается после подтверждения.",longButtons,0),false,1));
  var active=samples.Single(s=>s.Name=="Hunt").Screen;
  foreach(bool mini in new[]{false,true}){
   var sample=mini?samples.Single(s=>s.Name=="mini").Screen:active;
   samples.Add(($"elite-{mini}-short",NativeHuntPage.Hunt,sample with {Title="Manes",Elite=true},mini,0));
   samples.Add(($"elite-{mini}-long",NativeHuntPage.Hunt,sample with {Title=new string('Ж',100),Elite=true},mini,0));
  }
  var portraits=new Dictionary<uint,(string File,int Width,int Height)>();
  if(game!=null){
   foreach(uint icon in samples.Where(s=>!s.Mini&&s.Screen.Icon!=0).Select(s=>s.Screen.Icon).Distinct()){
    var tex=game.GetFile<Lumina.Data.Files.TexFile>($"ui/icon/{icon/1000*1000:D6}/{icon:D6}.tex")
     ??throw new InvalidDataException($"Missing preview portrait {icon}");
    string file=$"portrait-{icon}.bgra";
    File.WriteAllBytes(Path.Combine(directory,file),tex.ImageData);
    portraits.Add(icon,(file,tex.Header.Width,tex.Header.Height));
   }
  }
  foreach(var sample in samples){
   ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(900,1000);io.DeltaTime=1f/60;
   io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",17,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
   byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
   string title=NativeHuntLayout.FitLabel(sample.Screen.Title,110),summary=NativeHuntLayout.FitLabel(sample.Screen.Summary,420);
   ImGui.NewFrame();ImGui.Begin("measure");
   float Height(string value,float width,float font){ImGui.SetWindowFontScale(font/17f);return ImGui.CalcTextSize(value,false,width).Y+4;}
   float width=NativeHuntLayout.ContentWidth(sample.Mini);
   ImGui.SetWindowFontScale(NativeHuntLayout.TitleFont/17f);
   float titleWidth=ImGui.CalcTextSize(title).X;
   var layout=NativeHuntLayout.Build(sample.Screen,sample.Page,sample.Offset,Height(title,NativeHuntLayout.TitleWidth(sample.Screen,sample.Mini),NativeHuntLayout.TitleFont),Height(summary,NativeHuntLayout.SummaryWidth(sample.Screen,sample.Mini),NativeHuntLayout.SummaryFont(sample.Page,sample.Mini)),sample.Mini,label=>{ImGui.SetWindowFontScale(NativeHuntLayout.ButtonFont/17f);return Math.Max(ImGui.CalcTextSize(label).X,label.EnumerateRunes().Count()*9.5f);},titleWidth);
   var distinctDestinations=layout.Buttons.Where(b=>b.Button.Key.StartsWith("tab:")||b.Button.Key=="back")
    .Select(b=>b.Button.Action).Concat(sample.Screen.Buttons.Select(b=>b.Action));
   if(distinctDestinations.GroupBy(a=>a).Any(g=>g.Count()>1))
    throw new Exception("Duplicate control actions on "+sample.Name);
   if(sample.Name=="Hunt-empty"&&layout.Buttons.Count(b=>b.Button.Action is {Kind:"page",Argument:"Boards"})!=1)
    throw new Exception("Empty hunt must have one entry to Boards");
   if(sample.Page is NativeHuntPage.Target or NativeHuntPage.Filters && !layout.Buttons.Any(b=>b.Button.Key=="back"&&b.Button.Action.Argument=="Targets"))
    throw new Exception("Subpage lost its only return to the target list");
   if(layout.EliteBadge.HasValue!=sample.Screen.Elite)throw new Exception("Elite badge visibility mismatch");
   if(layout.EliteBadge is {} badge){
    if(badge.Position.X<layout.Heading.Position.X+layout.Heading.Size.X+NativeHuntLayout.EliteBadgeGap||
       badge.Position.X+badge.Size.X>layout.Summary.Position.X+NativeHuntLayout.SummaryWidth(sample.Screen,sample.Mini)||
       badge.Position.Y+badge.Size.Y>layout.Summary.Position.Y)
     throw new Exception("Elite badge collides with the heading, portrait or summary");
    if(sample.Name.EndsWith("-short")&&badge.Position.X>layout.Heading.Position.X+titleWidth+NativeHuntLayout.EliteBadgeGap+3)
     throw new Exception("Short elite badge is not adjacent to the name");
    ImGui.SetWindowFontScale(NativeHuntLayout.SecondaryFont/17f);
    if(ImGui.CalcTextSize(NativeHuntLayout.EliteLabel).X+8>badge.Size.X)
     throw new Exception("Elite badge label cannot fit");
   }
   if(sample.Name is "Actions-planned" or "Actions-next"){
    string kind=sample.Name=="Actions-planned"?"planned":"next";
    if(!layout.Buttons.Any(b=>b.Button.Action.Kind==kind&&b.Button.Key=="primary")||layout.Buttons.Any(b=>b.Button.Key=="resume"))
     throw new Exception("Pending travel must offer the correct teleport action");
    var focus=layout.Buttons.Single(b=>b.Button.Key=="focus");
    var targets=layout.Buttons.Single(b=>b.Button.Key=="targets");
    var nav=NativeHuntNavigation.Build(layout.Buttons.Select(b=>new NativeNavItem(b.Slot+1,b.Row,b.Column,b.Button.Key,b.Button.Enabled)));
    if(focus.Row!=targets.Row||nav.Single(n=>n.Key=="focus").Right!=targets.Slot+1||nav.Single(n=>n.Key=="targets").Left!=focus.Slot+1)
     throw new Exception("Focus and All targets must share a row and be adjacent on the D-pad");
   }
   var visibleRects=layout.Buttons.Select(b=>b.Rect).Concat(layout.TableText.Select(t=>t.Rect))
    .Append(layout.Heading).Append(layout.Summary);
   if(layout.EliteBadge is {} visibleBadge)visibleRects=visibleRects.Append(visibleBadge);
   if(sample.Screen.Progress!=null)visibleRects=visibleRects.Append(layout.ProgressLabel);
   if(!sample.Mini&&sample.Screen.Progress!=null)visibleRects=visibleRects.Append(layout.ProgressTrack);
   if(!sample.Mini&&sample.Screen.Icon!=0)visibleRects=visibleRects.Append(layout.Portrait);
   NativeResizeChecks.Check(layout.Size,visibleRects);
   if(sample.Mini&&sample.Name is "mini" or "mini-paused"){
    var actions=layout.Buttons.Where(b=>b.Button.Key is "primary" or "map" or "actions").ToArray();
    if(actions.Length!=3||actions.Select(b=>b.Row).Distinct().Count()!=1)
     throw new Exception("Mini panel split its action row");
   }
   foreach(var entry in layout.Buttons){
    if(entry.Button.Board==null&&entry.Button.Goal==null&&entry.Button.Checked==null&&sample.Name!="long-paged"){
     ImGui.SetWindowFontScale(NativeHuntLayout.ButtonFont/17f);
     if(ImGui.CalcTextSize(entry.Button.Label).X>entry.Rect.Size.X-16)
      throw new Exception("Native label exceeds real button insets: "+entry.Button.Label);
    }
    var r=entry.Rect;
    if(r.Position.X<0||r.Position.Y<0||r.Position.X+r.Size.X>layout.Size.X||r.Position.Y+r.Size.Y>layout.Size.Y)
     throw new Exception("Native button exceeds its window: "+entry.Button.Key);
   }
   var links=NativeHuntNavigation.Build(layout.Buttons.Select(b=>new NativeNavItem(b.Slot+1,b.Row,b.Column,b.Button.Key,b.Button.Enabled)));
   foreach(var startLink in links){
    var byIndex=links.ToDictionary(link=>link.Index);
    var visited=new HashSet<int>();
    var pending=new Queue<int>();
    pending.Enqueue(startLink.Index);
    while(pending.TryDequeue(out int index)){
     if(!visited.Add(index))continue;
     if(!byIndex.TryGetValue(index,out var link))throw new Exception("D-pad points to a hidden or disabled control: "+sample.Name);
     foreach(int next in new[]{link.Up,link.Down,link.Left,link.Right})pending.Enqueue(next);
    }
    if(visited.Count!=links.Length)throw new Exception("Unreachable control in "+sample.Name);
   }
   if(sample.Page==NativeHuntPage.Boards){
    if(layout.Buttons.Count(b=>b.Row==0)!=3||sample.Screen.Buttons.Any(b=>b.Action.Kind=="refresh"))
     throw new Exception("Boards must rely on background updates and show only three tabs");
    controller.Boards(true);
    if(controller.Build().Buttons.Count(b=>b.Action.Kind=="boards-all")!=1)
     throw new Exception("Filtered board view must still allow showing all boards");
    controller.Execute(new("boards-all"));
    if(controller.Build().Buttons.Any(b=>b.Action.Kind=="boards-all"))
     throw new Exception("Show all boards must disappear when already showing all boards");
    controller.Navigate(NativeHuntPage.Advanced);
    var refresh=controller.Build().Buttons.Single(b=>b.Action.Kind=="refresh");
    const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var nextScan=typeof(Plugin).GetField("nextScan",fields)!;
    var signature=typeof(Plugin).GetField("signature",fields)!;
    nextScan.SetValue(plugin,long.MaxValue);signature.SetValue(plugin,"stale");
    controller.Execute(refresh.Action);
    if(nextScan.GetValue(plugin) is not 0L||(string?)signature.GetValue(plugin)!="")
     throw new Exception("Advanced manual refresh must still schedule a fresh read");
    Console.WriteLine("PASS automatic boards layout and advanced manual refresh");
    var board=layout.Buttons.First(b=>b.Button.Enabled&&b.Button.Board!=null);
    var queue=new ConfirmationQueue();
    queue.Offer(new("travel","board-preview","К доске охоты?",()=>true,()=>true,()=>throw new Exception("Later must not teleport")));
    var dialog=queue.Activate(true)??throw new Exception("Board confirmation did not open");
    if(queue.Resolve(dialog.Key,false)!=null||queue.Active!=null||NativeHuntNavigation.RestoreFocus(links,board.Button.Key)!=board.Slot+1)
     throw new Exception("Dismissing confirmation must restore the board row by key");
    Console.WriteLine("PASS offline board dismissal and row focus restoration");
   }
   if(sample.Page==NativeHuntPage.Number){
    var steps=layout.Buttons.Where(b=>b.Button.Action.Kind=="step").ToArray();
    if(steps.Length!=6||steps.GroupBy(b=>b.Row).Select(g=>g.Count()).ToArray() is not [3,3]||steps.Any(b=>b.Rect.Size.X!=NativeHuntLayout.ActionWidth))
     throw new Exception("Numeric controls must fit two compact rows");
    for(int column=0;column<3;column++){
     var upper=links.Single(n=>n.Index==steps[column].Slot+1);
     var lower=links.Single(n=>n.Index==steps[column+3].Slot+1);
     if(upper.Down!=lower.Index||lower.Up!=upper.Index||
        upper.Right!=steps[(column+1)%3].Slot+1||lower.Left!=steps[3+(column+2)%3].Slot+1)
      throw new Exception("Numeric focus must follow the two-row grid");
    }
   }
   if(sample.Page==NativeHuntPage.Zones){
    var zones=layout.Buttons.Where(b=>b.Button.Action.Kind=="filter-zone").ToArray();
    if(zones.Length<2||zones.Any(b=>b.Button.Goal==null)||zones.Select(b=>b.Row).Distinct().Count()!=zones.Length)
     throw new Exception("Zones must be rendered as separate focusable rows");
   }
   ImGui.End();ImGui.EndFrame();io.DisplaySize=layout.Size;
   for(int frame=0;frame<3;frame++){
    ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(layout.Size);ImGui.Begin("Native layout preview",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings);
    var draw=ImGui.GetWindowDrawList();draw.AddRectFilled(Vector2.Zero,layout.Size,0xff231f1b,10);
    void Text(string value,NativeHuntRect rect,float font,Vector4 color){ImGui.SetWindowFontScale(font/17f);ImGui.SetCursorScreenPos(rect.Position);ImGui.PushTextWrapPos(rect.Position.X+rect.Size.X);ImGui.TextColored(color,value);ImGui.PopTextWrapPos();if(ImGui.GetItemRectMax().Y>rect.Position.Y+rect.Size.Y+2)throw new Exception("Native preview label exceeds block: "+sample.Name+" / "+value);}
    NativeHuntRect Center(string label,NativeHuntRect rect,float font){
     ImGui.SetWindowFontScale(font/17f);
     float inset=Math.Max(0,(rect.Size.X-ImGui.CalcTextSize(label).X)/2);
     return new(rect.Position+new Vector2(inset,0),rect.Size-new Vector2(inset,0));
    }
    Text(title,layout.Heading,NativeHuntLayout.TitleFont,sample.Screen.Progress!=null?NativeHuntColors.Tracking:Vector4.One);if(summary.Length>0)Text(summary,layout.Summary,NativeHuntLayout.SummaryFont(sample.Page,sample.Mini),sample.Screen.SummaryColor??NativeHuntColors.Muted);
    if(layout.EliteBadge is {} badgeRect){
     draw.AddRectFilled(badgeRect.Position,badgeRect.Position+badgeRect.Size,0xff30424d,4);
     draw.AddRect(badgeRect.Position,badgeRect.Position+badgeRect.Size,0xff427d9c,4);
     var labelRect=new NativeHuntRect(badgeRect.Position+new Vector2(0,2),badgeRect.Size-new Vector2(0,2));
     Text(NativeHuntLayout.EliteLabel,Center(NativeHuntLayout.EliteLabel,labelRect,NativeHuntLayout.SecondaryFont),NativeHuntLayout.SecondaryFont,NativeHuntLayout.EliteGold);
    }
    if(!sample.Mini&&sample.Screen.Icon!=0)draw.AddRectFilled(layout.Portrait.Position,layout.Portrait.Position+layout.Portrait.Size,0xff3b3631,6);
    if(sample.Screen.Progress is {} progress){
     var r=layout.ProgressTrack;
     Text(progress.Label,layout.ProgressLabel,NativeHuntLayout.SecondaryFont,new(.68f,.88f,.80f,1));
     if(r.Size.Y>0)draw.AddRectFilled(r.Position,r.Position+r.Size,0xff47453d,3);
     if(r.Size.Y>0)draw.AddRectFilled(r.Position,r.Position+new Vector2(r.Size.X*progress.Fraction,r.Size.Y),0xffa3c261,3);
    }
    if(layout.PageText.Length>0)Text(layout.PageText,Center(layout.PageText,layout.PageLabel,14),14,new(.9f,.9f,.9f,1));
    var focused=layout.Buttons.FirstOrDefault(b=>b.Button.Enabled&&(b.Button.Board!=null||b.Button.Goal!=null||b.Button.Checked.HasValue||b.Button.Key=="primary"));
    if(sample.Page==NativeHuntPage.Number){
     string key=sample.Name=="Number-lower"?"step:10":"step:-10";
     focused=layout.Buttons.Single(b=>b.Button.Key==key&&b.Button.Enabled);
    }
    if(sample.Page is NativeHuntPage.Advanced or NativeHuntPage.Filters)focused=layout.Buttons.FirstOrDefault(b=>b.Row==1&&b.Button.Enabled);
    foreach(var entry in layout.Buttons){
     var r=entry.Rect;
     if(entry.Button.Checked is {} state){
      var box=r.Position+new Vector2(0,2);
      draw.AddRect(box,box+new Vector2(20,20),0xffb0a697,3);
      if(state){
       draw.AddLine(box+new Vector2(4,10),box+new Vector2(8,15),ImGui.GetColorU32(NativeHuntColors.Ready),2);
       draw.AddLine(box+new Vector2(8,15),box+new Vector2(16,5),ImGui.GetColorU32(NativeHuntColors.Ready),2);
      }
      Text(entry.Button.Label,new(r.Position+new Vector2(24,2),new(r.Size.X-28,22)),NativeHuntLayout.BodyFont,focused?.Slot==entry.Slot?NativeHuntColors.Attention:Vector4.One);
      continue;
     }
     if(entry.Button.Board!=null||entry.Button.Goal!=null)continue;
     draw.AddRectFilled(r.Position,r.Position+r.Size,entry.Button.Key=="primary"&&entry.Button.Emphasized?0xff795f44:entry.Button.Enabled?0xff534a3a:0xff35312d,5);
     float font=entry.Button.Label.Contains('\n')||Height(entry.Button.Label,r.Size.X-16,NativeHuntLayout.ButtonFont)>r.Size.Y-8?NativeHuntLayout.BodyFont:NativeHuntLayout.ButtonFont;
     ImGui.SetWindowFontScale(font/17f);
     var textSize=ImGui.CalcTextSize(entry.Button.Label);
     float inset=entry.Button.Label.Contains('\n')?8:Math.Max(8,(r.Size.X-textSize.X)/2);
     var wrappedSize=ImGui.CalcTextSize(entry.Button.Label,false,r.Size.X-inset-8);
     Text(entry.Button.Label,new(r.Position+new Vector2(inset,(r.Size.Y-wrappedSize.Y)/2),new(r.Size.X-inset-8,wrappedSize.Y+2)),font,entry.Button.Enabled?(entry.Button.Emphasized?new(1,.85f,.55f,1):Vector4.One):new(.62f,.62f,.62f,1));
    }
    foreach(var cell in layout.TableText){
     ImGui.SetWindowFontScale(cell.Font/17f);
     if(ImGui.CalcTextSize(cell.Label).X>cell.Rect.Size.X&&sample.Page==NativeHuntPage.Boards)
      throw new Exception("Board cell label exceeds its column: "+cell.Label);
     Text(cell.Label,cell.Rect,cell.Font,cell.Color);
    }

    ImGui.End();ImGui.Render();
   }
   string atlas=$"huntroute-native-{sample.Name}.rgba";
   io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);
   File.WriteAllBytes(Path.Combine(directory,atlas),new ReadOnlySpan<byte>(pixels,tw*th*4).ToArray());
   var data=ImGui.GetDrawData();var lists=new List<object>();
   for(int i=0;i<data.CmdListsCount;i++){
    var list=new ImDrawListPtr(data.CmdLists[i]);var vertices=new List<double[]>();var indices=new List<ushort>();var commands=new List<object>();
    for(int n=0;n<list.VtxBuffer.Size;n++){var v=list.VtxBuffer[n];vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]);}
    for(int n=0;n<list.IdxBuffer.Size;n++)indices.Add(list.IdxBuffer[n]);
    for(int n=0;n<list.CmdBuffer.Size;n++){var c=list.CmdBuffer[n];commands.Add(new{Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W},Count=c.ElemCount,Offset=c.IdxOffset,VertexOffset=c.VtxOffset});}
    lists.Add(new{Vertices=vertices,Indices=indices,Commands=commands});
   }
   var portrait=portraits.GetValueOrDefault(sample.Screen.Icon);
   File.WriteAllText(Path.Combine(directory,$"huntroute-native-{sample.Name}.json"),JsonSerializer.Serialize(new{Width=(int)layout.Size.X,Height=(int)layout.Size.Y,Font=17,Atlas=atlas,Portrait=!sample.Mini&&sample.Screen.Icon!=0?new{Icon=sample.Screen.Icon,portrait.File,portrait.Width,portrait.Height,X=layout.Portrait.Position.X,Y=layout.Portrait.Position.Y,W=layout.Portrait.Size.X,H=layout.Portrait.Size.Y}:null,TextureWidth=tw,TextureHeight=th,Lists=lists}));
   ImGui.DestroyContext();Console.WriteLine("PASS native Hunt Route layout "+sample.Name);
  }
 }
}
