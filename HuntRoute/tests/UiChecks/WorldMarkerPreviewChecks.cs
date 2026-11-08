using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;

internal static class WorldMarkerPreviewChecks {
 internal static unsafe void Run(string directory){
  ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(800,450);io.DeltaTime=1f/60;
  io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",17,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
  byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
  var current=new WorldHuntMarker(10,Vector3.Zero,"Mourner",1,3,false,true,45);
  var ordinary=new WorldHuntMarker(11,Vector3.Zero,"Mourner",1,3,false,false,32);
  var elite=new WorldHuntMarker(12,Vector3.Zero,"Elite",0,1,true,false,74);
  for(int frame=0;frame<3;frame++){
   ImGui.NewFrame();var draw=ImGui.GetBackgroundDrawList();draw.AddRectFilled(Vector2.Zero,io.DisplaySize,0xff201c18);
   int before=draw.VtxBuffer.Size;WorldHuntPainter.Draw(current,new(float.NaN,100));if(draw.VtxBuffer.Size!=before)throw new Exception("Invalid projection emitted overlay vertices");
   WorldHuntPainter.Draw(current,new(260,140));WorldHuntPainter.Draw(ordinary,new(530,245));WorldHuntPainter.Draw(elite,new(320,350));
   if(draw.VtxBuffer.Size<=before||current.Color==ordinary.Color||current.Thickness<=ordinary.Thickness)throw new Exception("Current ordinary overlay has no distinct render treatment");
   ImGui.Render();
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
  File.WriteAllText(Path.Combine(directory,"huntroute-world-markers.json"),JsonSerializer.Serialize(new{Width=800,Height=450,Font=17,TextureWidth=tw,TextureHeight=th,Lists=lists}));
  ImGui.DestroyContext();Console.WriteLine("PASS ordinary/current/elite overlay composition and invalid-projection guard");
 }
}
