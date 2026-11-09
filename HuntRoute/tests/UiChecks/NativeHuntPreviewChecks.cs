using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;

// Offline check of actual native layout data; the game's Atk renderer/focus are not invoked.
internal static class NativeHuntPreviewChecks {
 internal static unsafe void Run(string directory,Plugin plugin){
  var controller=new NativeHuntController(plugin);
  var samples=new List<(string Name,NativeHuntPage Page,NativeHuntScreen Screen,bool Mini,int Offset)>();
  foreach(var page in Enum.GetValues<NativeHuntPage>()){controller.Navigate(page);samples.Add((page.ToString(),page,controller.Build(),false,0));}
  samples.Add(("mini",NativeHuntPage.Hunt,controller.Build(true),true,0));
  var active=new NativeHuntScreen("Swampmonk","Kozama’uka · Ежедневная цель\nМетка поставлена на карте",[
   new("primary","Пауза",new("pause"),Emphasized:true),new("map","Карта цели",new("map")),
   new("actions","Маршрут",new("page")),new("targets","Список целей",new("page"))
  ],123,new(1,3));
  samples.Add(("active",NativeHuntPage.Hunt,active,false,0));
  samples.Add(("active-mini",NativeHuntPage.Hunt,active,true,0));
  var longButtons=Enumerable.Range(0,13).Select(i=>new NativeHuntButton("goal:"+i,$"1/3 · {new string('Ж',100)}",new("target",i.ToString()),i!=6)).ToArray();
  samples.Add(("long-paged",NativeHuntPage.Targets,new(new string('Ж',100),"Текущая цель · длинная подпись\nСопровождение сохранено. Маршрут продолжается после подтверждения.",longButtons,123),false,1));
  foreach(var sample in samples){
   ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(900,1000);io.DeltaTime=1f/60;
   io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",17,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
   byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
   string title=NativeHuntLayout.FitLabel(sample.Screen.Title,110),summary=NativeHuntLayout.FitLabel(sample.Screen.Summary,420);
   ImGui.NewFrame();ImGui.Begin("measure");
   float Height(string value,float width,float font){ImGui.SetWindowFontScale(font/17f);return ImGui.CalcTextSize(value,false,width).Y+4;}
   float width=NativeHuntLayout.ContentWidth(sample.Mini);
   var layout=NativeHuntLayout.Build(sample.Screen,sample.Page,sample.Offset,Height(title,width,NativeHuntLayout.TitleFont),Height(summary,NativeHuntLayout.SummaryWidth(sample.Screen,sample.Mini),NativeHuntLayout.BodyFont),sample.Mini);
   ImGui.End();ImGui.EndFrame();io.DisplaySize=layout.Size;
   for(int frame=0;frame<3;frame++){
    ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(layout.Size);ImGui.Begin("Native layout preview",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings);
    var draw=ImGui.GetWindowDrawList();draw.AddRectFilled(Vector2.Zero,layout.Size,0xff231f1b,10);
    void Text(string value,NativeHuntRect rect,float font,Vector4 color){ImGui.SetWindowFontScale(font/17f);ImGui.SetCursorScreenPos(rect.Position);ImGui.PushTextWrapPos(rect.Position.X+rect.Size.X);ImGui.TextColored(color,value);ImGui.PopTextWrapPos();if(ImGui.GetItemRectMax().Y>rect.Position.Y+rect.Size.Y+2)throw new Exception("Native preview label exceeds block: "+sample.Name+" / "+value);}
    Text(title,layout.Heading,NativeHuntLayout.TitleFont,new(.96f,.90f,.75f,1));Text(summary,layout.Summary,NativeHuntLayout.BodyFont,new(.9f,.9f,.9f,1));
    if(!sample.Mini&&sample.Screen.Icon!=0)draw.AddRectFilled(layout.Portrait.Position,layout.Portrait.Position+layout.Portrait.Size,0xff3b3631,6);
    if(sample.Screen.Progress is {} progress){
     var r=layout.ProgressTrack;
     Text(progress.Label,layout.ProgressLabel,14,new(.68f,.88f,.80f,1));
     draw.AddRectFilled(r.Position,r.Position+r.Size,0xff47453d,3);
     draw.AddRectFilled(r.Position,r.Position+new Vector2(r.Size.X*progress.Fraction,r.Size.Y),0xffa3c261,3);
    }
    if(layout.PageText.Length>0)Text(layout.PageText,layout.PageLabel,14,new(.9f,.9f,.9f,1));
    foreach(var entry in layout.Buttons){
     var r=entry.Rect;draw.AddRectFilled(r.Position,r.Position+r.Size,entry.Button.Enabled?0xff534a3a:0xff35312d,5);
     float font=entry.Button.Label.Contains('\n')||Height(entry.Button.Label,r.Size.X-32,NativeHuntLayout.ButtonFont)>r.Size.Y-8?NativeHuntLayout.BodyFont:NativeHuntLayout.ButtonFont;
     Text(entry.Button.Label,new(r.Position+new Vector2(16,4),r.Size-new Vector2(32,8)),font,entry.Button.Enabled?(entry.Button.Emphasized?new(1,.85f,.55f,1):Vector4.One):new(.62f,.62f,.62f,1));
    }
    ImGui.End();ImGui.Render();
   }
   File.WriteAllBytes(Path.Combine(directory,"font-17.rgba"),new ReadOnlySpan<byte>(pixels,tw*th*4).ToArray());
   var data=ImGui.GetDrawData();var lists=new List<object>();
   for(int i=0;i<data.CmdListsCount;i++){
    var list=new ImDrawListPtr(data.CmdLists[i]);var vertices=new List<double[]>();var indices=new List<ushort>();var commands=new List<object>();
    for(int n=0;n<list.VtxBuffer.Size;n++){var v=list.VtxBuffer[n];vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]);}
    for(int n=0;n<list.IdxBuffer.Size;n++)indices.Add(list.IdxBuffer[n]);
    for(int n=0;n<list.CmdBuffer.Size;n++){var c=list.CmdBuffer[n];commands.Add(new{Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W},Count=c.ElemCount,Offset=c.IdxOffset,VertexOffset=c.VtxOffset});}
    lists.Add(new{Vertices=vertices,Indices=indices,Commands=commands});
   }
   File.WriteAllText(Path.Combine(directory,$"huntroute-native-{sample.Name}.json"),JsonSerializer.Serialize(new{Width=(int)layout.Size.X,Height=(int)layout.Size.Y,Font=17,TextureWidth=tw,TextureHeight=th,Lists=lists}));
   ImGui.DestroyContext();Console.WriteLine("PASS native Hunt Route layout "+sample.Name);
  }
 }
}
