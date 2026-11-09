using System;
using System.Collections.Generic;
using System.Numerics;
namespace HuntRoute;

public sealed record DialogMetric(string Label,string Value,bool Highlight=false);
public sealed record TravelDialogContent(string Eyebrow,string Title,string DestinationLabel,string Destination,string Context,string SubjectLabel,string Subject,string Detail,string Footer,DialogMetric[] Metrics,string? ActionLabel=null) {
    public string ConfirmLabel => ActionLabel ?? "Телепорт";
    public string DeclineLabel => "Позже";
    public static TravelDialogContent Login(string mode) => mode switch {
        "taken"=>Notice("Продолжить охоту?","Продолжить"),
        "pickup"=>Pickup(),
        _=>Notice("Открыть сводку охоты?","Открыть","Новых контрактов пока нет.")
    };
    public static TravelDialogContent Pickup() => Notice("Доступны новые контракты","К доскам");
    private static TravelDialogContent Notice(string title,string action,string context="") =>
        new("",title,"","",context,"","","","",[],action);
    public bool HasSubject => Subject.Length>0 || Detail.Length>0;
    public static TravelDialogContent Next(string from,string zone,string target) => new(
        "Следующая зона",zone,"",zone,
        "Завершены цели: "+from,"Первая цель",target,"",
        "",[]);
    public static TravelDialogContent Board(string city,string aetheryte,string expansion,uint gil,bool local) => new(
        "Доска охоты",city,"",city,
        local?"Вы уже в городе":$"{(aetheryte==city?"":aetheryte+" · ")}{gil} гил",
        "",expansion,"","",[],local?"К доске":null);
    public static TravelDialogContent Shortcut(ShortcutOffer offer,string zone,string target) => new(
        "","Сократить перелёт?","",offer.Name,
        $"{zone} · {offer.GilCost} гил","Цель",target,$"От эфирита ~{offer.FinalDistance:F0} м",
        "Каст и загрузка учтены.",
        [new("Перелёт",$"~{offer.DirectSeconds:F0} с"),new("Телепорт",$"~{offer.TeleportSeconds:F0} с"),new("Экономия",$"~{offer.SavedSeconds:F0} с",true)]);
    public static TravelDialogContent Plain(string text) => Notice(string.IsNullOrWhiteSpace(text)?"Продолжить?":text,"Продолжить");
}
public sealed record DialogText(string Text,Vector2 Position,Vector2 Size,uint Font,Vector4 Color);
public sealed record DialogButton(Vector2 Position,Vector2 Size);
public sealed record TravelDialogLayout(float Width,float Height,DialogButton Confirm,DialogButton Decline,List<DialogText> Texts) {
    public static readonly Vector4 Brand=new(.72f,.83f,1,1),Mint=new(.55f,.96f,.80f,1),Muted=new(.83f,.86f,.91f,1),White=new(.94f,.96f,1,1);
    public const float DialogWidth=400;
    public static float FitScale(Vector2 size,Vector2 viewport,float requested) {
        if(!float.IsFinite(requested)||requested<=0)requested=1;
        if(viewport.X<=16||viewport.Y<=16)return requested;
        return Math.Min(requested,Math.Min((viewport.X-16)/size.X,(viewport.Y-16)/size.Y));
    }
    public static TravelDialogLayout Build(TravelDialogContent content,Func<string,float,uint,float> measure) {
        const float width=DialogWidth,left=18,inner=width-2*left;
        var texts=new List<DialogText>();
        float Text(string value,float x,float y,float w,uint font,Vector4 color) {
            float height=Math.Max(font+3,measure(value,w,font));
            if(!float.IsFinite(height) || height<0)throw new ArgumentException("Invalid text measurement");
            texts.Add(new(value,new(x,y),new(w,height),font,color));return height;
        }
        float y=38;
        if(content.Eyebrow.Length>0)y+=Text(content.Eyebrow,left,y,inner,13,Brand)+2;
        y+=Text(content.Title,left,y,inner,NativeHuntLayout.TitleFont,White)+4;
        if(!string.IsNullOrEmpty(content.Destination)&&content.Destination!=content.Title) {
            string destination=string.IsNullOrEmpty(content.DestinationLabel)?content.Destination:$"{content.DestinationLabel} · {content.Destination}";
            y+=Text(destination,left,y,inner,NativeHuntLayout.BodyFont,White)+2;
        }
        if(!string.IsNullOrEmpty(content.Context)) y+=Text(content.Context,left,y,inner,NativeHuntLayout.BodyFont,Mint)+4;
        if(content.HasSubject) {
            y+=2;
            string subject=string.IsNullOrEmpty(content.SubjectLabel)?content.Subject:$"{content.SubjectLabel} · {content.Subject}";
            y+=Text(subject,left,y,inner,NativeHuntLayout.BodyFont,Brand);
            if(!string.IsNullOrEmpty(content.Detail)) y+=4+Text(content.Detail,left,y+4,inner,13,Muted);
            y+=4;
        }
        if(content.Metrics.Length>0) {
            float cardWidth=(inner-(content.Metrics.Length-1)*8)/content.Metrics.Length,rowHeight=0;
            for(int i=0;i<content.Metrics.Length;i++) {
                var metric=content.Metrics[i];float x=left+i*(cardWidth+8),my=y+4;
                my+=Text(metric.Label,x,my,cardWidth,13,Muted)+2;
                my+=Text(metric.Value,x,my,cardWidth,NativeHuntLayout.BodyFont,metric.Highlight?Mint:White)+4;
                rowHeight=Math.Max(rowHeight,my-y);
            }
            y+=rowHeight+4;
        }
        if(!string.IsNullOrEmpty(content.Footer)) y+=Text(content.Footer,left,y,inner,13,Muted)+4;
        const float buttonHeight=NativeHuntLayout.ActionHeight;
        float confirmWidth=NativeHuntLayout.ButtonWidth(content.ConfirmLabel);
        float buttonsY=y+6,heightTotal=buttonsY+buttonHeight+12;
        float buttonsX=(width-confirmWidth-NativeHuntLayout.ActionWidth-6)/2;
        var confirm=new DialogButton(new(buttonsX,buttonsY),new(confirmWidth,buttonHeight));
        var decline=new DialogButton(new(buttonsX+confirmWidth+6,buttonsY),new(NativeHuntLayout.ActionWidth,buttonHeight));
        return new(width,heightTotal,confirm,decline,texts);
    }
}
