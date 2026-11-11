using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using HuntRoute;

// Offline composition preview of the shared native layout; not a screenshot of FFXIV's Atk renderer.
internal static class DialogPreviewChecks {
    internal static unsafe void Run(string directory) {
        var offer=new ShortcutOffer(1,10,0,1,815,1,"Mord Souq",100,1094,440,54.7f,40);
        foreach(var sample in new[]{("login",TravelDialogContent.Login("taken")),("login-summary",TravelDialogContent.Login("summary")),("pickup",TravelDialogContent.Pickup()),("board",TravelDialogContent.Board("Old Sharlayan","Old Sharlayan","Endwalker",1235,false)),("board-free",TravelDialogContent.Board("Old Sharlayan","Old Sharlayan","Endwalker",0,false)),("board-separate-aetheryte",TravelDialogContent.Board("The Crystarium","The Dossal Gate","Shadowbringers",1235,false)),("board-local",TravelDialogContent.Board("Old Sharlayan","Old Sharlayan","Endwalker",0,true)),("plain",TravelDialogContent.Plain("Продолжить?")),("plain-long",TravelDialogContent.Plain(string.Concat(Enumerable.Repeat("Продолжить охоту по оставшимся контрактам? ",4)))),("next",TravelDialogContent.Next("The Ruby Sea","The Rak'tika Greatwood","gizamaluk") with {Context="Fanow · 354 гил"}),("next-portrait",TravelDialogContent.Next("The Ruby Sea","Mare Lamentorum","caretaker",1) with {Context="Bestways Burrow · 1854 гил"}),("next-portrait-long",TravelDialogContent.Next("The Ruby Sea","The Dravanian Hinterlands",new string('Ж',100),1)),("shortcut",TravelDialogContent.Shortcut(offer,"Amh Araeng","gnome")),("long",TravelDialogContent.Next("Coerthas Western Highlands","The Dravanian Hinterlands",new string('Ж',100)))}) {
            ImGui.CreateContext();var io=ImGui.GetIO();io.IniFilename=null;io.DisplaySize=new(520,900);io.DeltaTime=1f/60;
            io.Fonts.AddFontFromFileTTF("C:/Windows/Fonts/segoeui.ttf",17,(ImFontConfig*)null,io.Fonts.GetGlyphRangesCyrillic());
            byte* pixels;int tw,th;io.Fonts.GetTexDataAsRGBA32(0,&pixels,&tw,&th);io.Fonts.SetTexID(0,new ImTextureID(1));
            ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);ImGui.Begin("measure",ImGuiWindowFlags.NoDecoration);
            var layout=TravelDialogLayout.Build(sample.Item2,(text,width,font)=> { ImGui.SetWindowFontScale(font/17f);return ImGui.CalcTextSize(text,false,width).Y+4; },label=>{ImGui.SetWindowFontScale(NativeHuntLayout.ButtonFont/17f);return Math.Max(ImGui.CalcTextSize(label).X,label.EnumerateRunes().Count()*9.5f);});
            if(sample.Item1=="board"&&!layout.Texts.Select(t=>t.Text).SequenceEqual(new[]{"Доска охоты · Endwalker","Old Sharlayan","Стоимость телепорта: 1\u00A0235 гил"}))
                throw new Exception("Board prompt must identify the expansion, city and price once");
            if(sample.Item1=="board-local"&&(sample.Item2.ConfirmLabel!="К доске"||layout.Texts.Any(t=>t.Text.Contains("гил"))))
                throw new Exception("Local board prompt must offer a marker without a fare");
            bool compact=sample.Item1 is "login" or "login-summary" or "pickup" or "plain" or "board" or "board-free" or "board-local";
            if(layout.Texts.Any(t=>t.Centered!=compact || (t.Centered&&Math.Abs(t.Position.X+t.Size.X/2-layout.Width/2)>0.5f)))
                throw new Exception("Unexpected dialog text alignment: "+sample.Item1);
            if(layout.Confirm.Position.Y!=layout.Decline.Position.Y||Math.Abs(layout.Confirm.Position.X+layout.Decline.Position.X+layout.Decline.Size.X-layout.Width)>0.5f)
                throw new Exception("Dialog buttons must form a centered row: "+sample.Item1);
            if(layout.Portrait.HasValue!=(sample.Item2.PortraitIcon!=0))throw new Exception("Dialog portrait visibility mismatch");
            if(layout.Portrait is {} portrait){
                NativeResizeChecks.Check(new(layout.Width,layout.Height),[portrait]);
                if(layout.Texts.Any(t=>t.Position.X+t.Size.X>portrait.Position.X&&t.Position.Y<portrait.Position.Y+portrait.Size.Y)
                    ||layout.Confirm.Position.Y<portrait.Position.Y+portrait.Size.Y)
                    throw new Exception("Dialog portrait overlaps text or buttons");
                var tall=portrait.FitImage(new(32,128));
                if(tall.Size.X!=24||tall.Size.Y!=96)throw new Exception("Portrait aspect ratio changed");
            }
            NativeResizeChecks.Check(new(layout.Width,layout.Height),
                layout.Texts.Select(t=>new NativeHuntRect(t.Position,t.Size))
                .Append(new(layout.Confirm.Position,layout.Confirm.Size)).Append(new(layout.Decline.Position,layout.Decline.Size)));

            ImGui.End();ImGui.EndFrame();io.DisplaySize=new(layout.Width,MathF.Ceiling(layout.Height));
            for(int frame=0;frame<3;frame++) {
                ImGui.NewFrame();ImGui.SetNextWindowPos(Vector2.Zero);ImGui.SetNextWindowSize(io.DisplaySize);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);ImGui.Begin("composition",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoSavedSettings);
                var draw=ImGui.GetWindowDrawList();draw.AddRectFilled(Vector2.Zero,io.DisplaySize,ImGui.GetColorU32(new Vector4(.25f,.25f,.25f,1)),12);
                ImGui.SetWindowFontScale(14f/17);ImGui.SetCursorScreenPos(new(18,9));ImGui.TextColored(TravelDialogLayout.White,"Hunt Route");
                foreach(var text in layout.Texts) {
                    ImGui.SetWindowFontScale(text.Font/17f);
                    var position=text.Position;
                    if(text.Centered) {
                        float measured=ImGui.CalcTextSize(text.Text).X;
                        if(measured>text.Size.X)throw new Exception("Centered caption exceeds its width: "+text.Text);
                        position.X+=(text.Size.X-measured)/2;
                    }
                    ImGui.SetCursorScreenPos(position);ImGui.PushTextWrapPos(text.Position.X+text.Size.X);
                    ImGui.TextColored(text.Color,text.Text);ImGui.PopTextWrapPos();
                    if(ImGui.GetItemRectMax().Y>text.Position.Y+text.Size.Y+1)throw new Exception("Dialog text exceeds its measured block");
                }
                ImGui.SetWindowFontScale(NativeHuntLayout.ButtonFont/17f);
                foreach(var (rect,label,primary) in new[]{(layout.Confirm,sample.Item2.ConfirmLabel,true),(layout.Decline,sample.Item2.DeclineLabel,false)}) {
                    var size=ImGui.CalcTextSize(label);
                    if(size.X>rect.Size.X-16||rect.Position.Y+rect.Size.Y>layout.Height-12||rect.Position.X+rect.Size.X>layout.Width-16)
                        throw new Exception("Dialog button exceeds its bounds: "+label);
                    draw.AddRectFilled(rect.Position,rect.Position+rect.Size,ImGui.GetColorU32(primary?new Vector4(.26f,.38f,.49f,1):new Vector4(.31f,.31f,.31f,1)),5);
                    ImGui.SetCursorScreenPos(rect.Position+(rect.Size-size)/2);ImGui.TextColored(primary?NativeHuntColors.Attention:TravelDialogLayout.White,label);
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
