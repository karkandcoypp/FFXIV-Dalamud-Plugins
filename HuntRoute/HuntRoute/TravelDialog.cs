using System;
using System.Collections.Generic;
using System.Numerics;
namespace HuntRoute;

public sealed record DialogMetric(string Label,string Value,bool Highlight=false);
public sealed record TravelDialogContent(string Eyebrow,string Title,string DestinationLabel,string Destination,string Context,string SubjectLabel,string Subject,string Detail,string Footer,DialogMetric[] Metrics,string? ActionLabel=null) {
    public string ConfirmLabel => ActionLabel ?? "Телепортироваться";
    public string DeclineLabel => "Позже";
    public static TravelDialogContent Next(string from,string zone,string target) => new(
        "HUNT ROUTE · СЛЕДУЮЩАЯ ЗОНА","Продолжить охоту?","КУДА",zone,
        "Завершены цели: "+from,"ПЕРВАЯ ЦЕЛЬ",target,"Метка появится после прибытия.",
        "Сопровождение продолжится автоматически.",[]);
    public static TravelDialogContent Shortcut(ShortcutOffer offer,string zone,string target) => new(
        "HUNT ROUTE · В ПРЕДЕЛАХ ЗОНЫ","Сократить перелёт?","ЭФИРИТ",offer.Name,
        $"{zone} · {offer.GilCost} гил","ЦЕЛЬ",target,$"После телепорта до цели ~{offer.FinalDistance:F0} м",
        "Каст и загрузка учтены. Время приблизительное.\nМетка и сопровождение сохранятся.",
        [new("НАПРЯМУЮ",$"~{offer.DirectSeconds:F0} с"),new("С ТЕЛЕПОРТОМ",$"~{offer.TeleportSeconds:F0} с"),new("ЭКОНОМИЯ",$"~{offer.SavedSeconds:F0} с",true)]);
    public static TravelDialogContent Plain(string text) => new("HUNT ROUTE","Подтверждение","","","","ДЕЙСТВИЕ",string.IsNullOrWhiteSpace(text)?"Продолжить?":text,"","",[]);
}
public sealed record DialogText(string Text,Vector2 Position,Vector2 Size,uint Font,Vector4 Color);
public sealed record DialogPanel(Vector2 Position,Vector2 Size,Vector4 Color);
public sealed record TravelDialogLayout(float Width,float Height,float ButtonsY,List<DialogText> Texts,List<DialogPanel> Panels) {
    public static readonly Vector4 Brand=new(.57f,.65f,1,1),Mint=new(.37f,.85f,.70f,1),Muted=new(.62f,.68f,.78f,1),White=new(.94f,.96f,1,1);
    public static TravelDialogLayout Build(TravelDialogContent content,Func<string,float,uint,float> measure) {
        const float width=520,left=32,inner=456;
        var texts=new List<DialogText>();var panels=new List<DialogPanel> {new(new(16,36),Vector2.Zero,new(.055f,.065f,.095f,1)),new(new(32,42),Vector2.Zero,Brand)};
        float Text(string value,float x,float y,float w,uint font,Vector4 color) {
            float height=Math.Max(font+4,measure(value,w,font));
            if(!float.IsFinite(height) || height<0)throw new ArgumentException("Invalid text measurement");
            texts.Add(new(value,new(x,y),new(w,height),font,color));return height;
        }
        float y=42;y+=Text(content.Eyebrow,44,y,444,11,Brand)+4;
        y+=Text(content.Title,44,y,444,22,White)+16;
        panels[1]=panels[1] with {Size=new(3,y-42-8)};
        if(!string.IsNullOrEmpty(content.Destination)) {
            y+=Text(content.DestinationLabel,left,y,inner,11,Muted)+3;
            y+=Text(content.Destination,left,y,inner,24,White)+6;
        }
        if(!string.IsNullOrEmpty(content.Context)) y+=Text(content.Context,left,y,inner,13,Mint)+14;
        float cardTop=y;y+=12;
        y+=Text(content.SubjectLabel,left+12,y,inner-24,11,Muted)+4;
        y+=Text(content.Subject,left+12,y,inner-24,17,White);
        if(!string.IsNullOrEmpty(content.Detail)) y+=4+Text(content.Detail,left+12,y+4,inner-24,12,Muted);
        y+=12;panels.Add(new(new(left,cardTop),new(inner,y-cardTop),new(.10f,.125f,.18f,1)));y+=12;
        if(content.Metrics.Length>0) {
            float cardWidth=(inner-(content.Metrics.Length-1)*8)/content.Metrics.Length,rowTop=y,rowHeight=0;
            for(int i=0;i<content.Metrics.Length;i++) {
                var metric=content.Metrics[i];float x=left+i*(cardWidth+8),my=y+10;
                my+=Text(metric.Label,x+10,my,cardWidth-20,10,Muted)+4;
                my+=Text(metric.Value,x+10,my,cardWidth-20,20,metric.Highlight?Mint:White)+10;
                rowHeight=Math.Max(rowHeight,my-y);
                panels.Add(new(new(x,rowTop),Vector2.Zero,metric.Highlight?new(.075f,.19f,.17f,1):new(.10f,.125f,.18f,1)));
            }
            for(int i=panels.Count-content.Metrics.Length;i<panels.Count;i++)panels[i]=panels[i] with {Size=new(cardWidth,rowHeight)};
            y+=rowHeight+14;
        }
        if(!string.IsNullOrEmpty(content.Footer)) y+=Text(content.Footer,left,y,inner,12,Muted)+14;
        panels.Add(new(new(left,y),new(inner,1),new(.22f,.27f,.36f,1)));
        float buttons=y+16,heightTotal=buttons+62;
        panels[0]=panels[0] with {Size=new(width-32,heightTotal-52)};
        return new(width,heightTotal,buttons,texts,panels);
    }
}
