using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;

// Offline composition preview of the shared native layout; not a screenshot of FFXIV's Atk renderer.
internal static class DialogPreviewChecks {
    internal static unsafe void Run(string directory) {
        var offer=new ShortcutOffer(1,10,0,1,815,1,"Mord Souq",100,1094,440,54.7f,40);
        foreach(var sample in new[]{("login",TravelDialogContent.Login("taken")),("login-summary",TravelDialogContent.Login("summary")),("pickup",TravelDialogContent.Pickup()),("board",new TravelDialogContent("HUNT ROUTE · ДОСКА","За новыми контрактами?","ГОРОД","Old Sharlayan","Old Sharlayan · 100 гил","КОНТРАКТЫ","Endwalker","После прибытия будет поставлена метка доски.","Текущий маршрут сохраняется.",[])),("plain",TravelDialogContent.Plain("Продолжить?")),("next",TravelDialogContent.Next("The Ruby Sea","The Rak'tika Greatwood","gizamaluk") with {Context="Fanow · 354 гил"}),("shortcut",TravelDialogContent.Shortcut(offer,"Amh Araeng","gnome")),("long",TravelDialogContent.Next("Coerthas Western Highlands","The Dravanian Hinterlands",new string('Ж',100)))}) {
            ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(520,900);io.DeltaTime=1f/60;
            io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",17,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
            byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
            ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);ImGui.Begin("measure",ImGuiWindowFlags.NoDecoration);
            var layout=TravelDialogLayout.Build(sample.Item2,(text,width,font)=> { ImGui.SetWindowFontScale(font/17f);return ImGui.CalcTextSize(text,false,width).Y+4; });
            ImGui.End();ImGui.EndFrame();io.DisplaySize=new(layout.Width,MathF.Ceiling(layout.Height));
            for(int frame=0;frame<3;frame++) {
                ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);ImGui.Begin("composition",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoSavedSettings);
                var draw=ImGui.GetWindowDrawList();draw.AddRectFilled(Vector2.Zero,io.DisplaySize,ImGui.GetColorU32(new Vector4(.25f,.25f,.25f,1)),12);
                ImGui.SetWindowFontScale(14f/17);ImGui.SetCursorScreenPos(new(18,9));ImGui.TextColored(TravelDialogLayout.White,"Hunt Route");
                foreach(var text in layout.Texts) {
                    ImGui.SetWindowFontScale(text.Font/17f);ImGui.SetCursorScreenPos(text.Position);ImGui.PushTextWrapPos(text.Position.X+text.Size.X);
                    ImGui.TextColored(text.Color,text.Text);ImGui.PopTextWrapPos();
                    if(ImGui.GetItemRectMax().Y>text.Position.Y+text.Size.Y+1)throw new Exception("Dialog text exceeds its measured block");
                }
                ImGui.SetWindowFontScale(NativeHuntLayout.ButtonFont/17f);
                foreach(var (rect,label,primary) in new[]{(layout.Confirm,sample.Item2.ConfirmLabel,true),(layout.Decline,sample.Item2.DeclineLabel,false)}) {
                    var size=ImGui.CalcTextSize(label);
                    if(size.X>rect.Size.X-16||rect.Position.Y+rect.Size.Y>layout.Height-12||rect.Position.X+rect.Size.X>layout.Width-16)
                        throw new Exception("Dialog button exceeds its bounds: "+label);
                    draw.AddRectFilled(rect.Position,rect.Position+rect.Size,ImGui.GetColorU32(primary?new Vector4(.36f,.36f,.36f,1):new Vector4(.31f,.31f,.31f,1)),5);
                    ImGui.SetCursorScreenPos(rect.Position+(rect.Size-size)/2);ImGui.TextColored(TravelDialogLayout.White,label);
                }
                ImGui.End();ImGui.PopStyleVar();ImGui.Render();
            }
            File.WriteAllBytes(Path.Combine(directory,"font-17.rgba"),new ReadOnlySpan<byte>(pixels,tw*th*4).ToArray());
            var data=ImGui.GetDrawData();var lists=new List<object>();
            for(int i=0;i<data.CmdListsCount;i++) {
                var list=new ImDrawListPtr(data.CmdLists[i]);var vertices=new List<double[]>();var indices=new List<ushort>();var commands=new List<object>();
                for(int n=0;n<list.VtxBuffer.Size;n++){var v=list.VtxBuffer[n];vertices.Add([v.Pos.X,v.Pos.Y,v.Uv.X,v.Uv.Y,v.Col]);}
                for(int n=0;n<list.IdxBuffer.Size;n++)indices.Add(list.IdxBuffer[n]);
                for(int n=0;n<list.CmdBuffer.Size;n++){var c=list.CmdBuffer[n];commands.Add(new{Clip=new[]{c.ClipRect.X,c.ClipRect.Y,c.ClipRect.Z,c.ClipRect.W},Count=c.ElemCount,Offset=c.IdxOffset,VertexOffset=c.VtxOffset});}
                lists.Add(new{Vertices=vertices,Indices=indices,Commands=commands});
            }
            File.WriteAllText(Path.Combine(directory,$"huntroute-dialog-{sample.Item1}.json"),JsonSerializer.Serialize(new{Width=(int)io.DisplaySize.X,Height=(int)io.DisplaySize.Y,Font=17,TextureWidth=tw,TextureHeight=th,Lists=lists}));
            Console.WriteLine($"PASS native dialog composition {sample.Item1}: {layout.Width}x{layout.Height}");ImGui.DestroyContext();
        }
    }
}
