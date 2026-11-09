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
   foreach(var entry in layout.Buttons){
    var r=entry.Rect;
    if(r.Position.X<0||r.Position.Y<0||r.Position.X+r.Size.X>layout.Size.X||r.Position.Y+r.Size.Y>layout.Size.Y)
     throw new Exception("Native button exceeds its window: "+entry.Button.Key);
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
    Text(title,sample.Page==NativeHuntPage.Settings?Center(title,layout.Heading,NativeHuntLayout.TitleFont):layout.Heading,NativeHuntLayout.TitleFont,sample.Screen.Progress!=null?NativeHuntColors.Tracking:Vector4.One);if(summary.Length>0)Text(summary,layout.Summary,NativeHuntLayout.BodyFont,sample.Screen.SummaryColor??NativeHuntColors.Muted);
    if(!sample.Mini&&sample.Screen.Icon!=0)draw.AddRectFilled(layout.Portrait.Position,layout.Portrait.Position+layout.Portrait.Size,0xff3b3631,6);
    if(sample.Screen.Progress is {} progress){
     var r=layout.ProgressTrack;
     Text(progress.Label,layout.ProgressLabel,NativeHuntLayout.BodyFont,new(.68f,.88f,.80f,1));
     draw.AddRectFilled(r.Position,r.Position+r.Size,0xff47453d,3);
     draw.AddRectFilled(r.Position,r.Position+new Vector2(r.Size.X*progress.Fraction,r.Size.Y),0xffa3c261,3);
    }
    if(layout.PageText.Length>0)Text(layout.PageText,Center(layout.PageText,layout.PageLabel,14),14,new(.9f,.9f,.9f,1));
    foreach(var entry in layout.Buttons){
     var r=entry.Rect;
     if(entry.Button.Checked is {} state){
      var box=r.Position+new Vector2(r.Size.X-28,2);
      draw.AddRect(box,box+new Vector2(20,20),0xffb0a697,3);
      if(state){
       draw.AddLine(box+new Vector2(4,10),box+new Vector2(8,15),ImGui.GetColorU32(NativeHuntColors.Ready),2);
       draw.AddLine(box+new Vector2(8,15),box+new Vector2(16,5),ImGui.GetColorU32(NativeHuntColors.Ready),2);
      }
      Text(entry.Button.Label,new(r.Position+new Vector2(8,2),new(r.Size.X-40,22)),NativeHuntLayout.BodyFont,Vector4.One);
      continue;
     }
     if(entry.Button.Board==null)draw.AddRectFilled(r.Position,r.Position+r.Size,entry.Button.Enabled?0xff534a3a:0xff35312d,5);

     float font=entry.Button.Label.Contains('\n')||Height(entry.Button.Label,r.Size.X-32,NativeHuntLayout.ButtonFont)>r.Size.Y-8?NativeHuntLayout.BodyFont:NativeHuntLayout.ButtonFont;
     if(entry.Button.Board is {} row){
      Text(row.Name,new(r.Position+new Vector2(16,4),new(r.Size.X-32,18)),font,NativeHuntColors.Tracking);
      ImGui.SetWindowFontScale(font/17f);
      void StatusLine(string prefix,NativeBoardStatus status,float y){
       float x=r.Position.X+16;
       float prefixWidth=ImGui.CalcTextSize(prefix).X;
       float statusWidth=ImGui.CalcTextSize(status.Label).X;
       if(x+prefixWidth+statusWidth>r.Position.X+r.Size.X-16||y+18>r.Position.Y+r.Size.Y-4)
        throw new Exception("Board status line exceeds its button");
       Text(prefix,new(new(x,y),new(prefixWidth+2,18)),font,Vector4.One);
       Text(status.Label,new(new(x+prefixWidth,y),new(statusWidth+2,18)),font,status.Color??Vector4.One);
      }
      StatusLine(NativeBoardRow.DailyPrefix,row.Daily,r.Position.Y+22);
      StatusLine(NativeBoardRow.WeeklyPrefix,row.Weekly,r.Position.Y+38);
      continue;
     }
     ImGui.SetWindowFontScale(font/17f);
     var textSize=ImGui.CalcTextSize(entry.Button.Label);
     float inset=entry.Button.Label.Contains('\n')?16:Math.Max(16,(r.Size.X-textSize.X)/2);
     Text(entry.Button.Label,new(r.Position+new Vector2(inset,4),new(r.Size.X-inset-8,r.Size.Y-8)),font,entry.Button.Enabled?(entry.Button.Emphasized?new(1,.85f,.55f,1):Vector4.One):new(.62f,.62f,.62f,1));
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
