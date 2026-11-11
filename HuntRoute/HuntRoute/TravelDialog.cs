using System;
using System.Collections.Generic;
using System.Numerics;
namespace HuntRoute;

public sealed record DialogMetric(string Label,string Value,bool Highlight=false);
public sealed record TravelDialogContent(string Eyebrow,string Title,string DestinationLabel,string Destination,string Context,string SubjectLabel,string Subject,string Detail,string Footer,DialogMetric[] Metrics,string? ActionLabel=null,uint PortraitIcon=0,bool CenterNoticeText=false,bool NextZone=false,string Summary="",uint? Fare=null) {
    public string FareLabel => Fare is {} gil?$"{gil} гил":"";
    public string ConfirmLabel => ActionLabel ?? "Телепорт";
    public string DeclineLabel => "Позже";
    public static TravelDialogContent Login(string mode) => mode switch {
        "taken"=>Notice("Продолжить охоту?","Продолжить"),
        "pickup"=>Pickup(),
        _=>Notice("Открыть сводку охоты?","Открыть","Новых контрактов пока нет.")
    };
    public static TravelDialogContent Pickup() => Notice("Доступны новые контракты","К доскам");
    private static TravelDialogContent Notice(string title,string action,string context="") =>
        new("",title,"","",context,"","","","",[],action,CenterNoticeText:true);
    public bool HasSubject => Subject.Length>0 || Detail.Length>0;
    public static TravelDialogContent Next(string from,string zone,string target,uint portraitIcon=0) => new(
        "Следующая зона",zone,"",zone,
        "Завершены цели: "+from,"Первая цель",target,"",
        "",[],PortraitIcon:portraitIcon,NextZone:true);
    public static TravelDialogContent Board(string city,string aetheryte,string expansion,uint gil,bool local) => new(
        $"Доска охоты · {expansion}",city,"Эфирит",local||aetheryte==city?"":aetheryte,
        local?"Вы уже в городе · поставим метку":gil==0?"Телепорт бесплатный":$"Стоимость телепорта: {gil.ToString("N0",System.Globalization.CultureInfo.InvariantCulture).Replace(',', '\u00A0')} гил",
        "","","","",[],local?"К доске":null,CenterNoticeText:local||aetheryte==city);
    public static TravelDialogContent Shortcut(ShortcutOffer offer,string zone,string target) => new(
        "","Сократить перелёт?","Эфирит",offer.Name,
        zone,"Цель",target,$"≈{offer.FinalDistance:F0} м от эфирита",
        "",
        [new("Напрямую",$"≈{offer.DirectSeconds:F0} с"),new("С телепортом",$"≈{offer.TeleportSeconds:F0} с")],
        Summary:$"Экономия ≈{offer.SavedSeconds:F0} с · {offer.GilCost} гил");
    public static TravelDialogContent Plain(string text) => Notice(string.IsNullOrWhiteSpace(text)?"Продолжить?":text,"Продолжить");
}
public sealed record DialogText(string Text,Vector2 Position,Vector2 Size,uint Font,Vector4 Color,bool Centered=false);
public sealed record DialogButton(Vector2 Position,Vector2 Size);
public sealed record TravelDialogLayout(float Width,float Height,DialogButton Confirm,DialogButton Decline,List<DialogText> Texts,NativeHuntRect? Portrait) {
    public static readonly Vector4 Brand=new(.72f,.83f,1,1),Mint=new(.55f,.96f,.80f,1),Muted=new(.83f,.86f,.91f,1),White=new(.94f,.96f,1,1);
    public const float DialogWidth=400;
    public static float FitScale(Vector2 size,Vector2 viewport,float requested) {
        if(!float.IsFinite(requested)||requested<=0)requested=1;
        if(viewport.X<=16||viewport.Y<=16)return requested;
        return Math.Min(requested,Math.Min((viewport.X-16)/size.X,(viewport.Y-16)/size.Y));
    }
    public static Vector2 ClampPosition(Vector2 origin,Vector2 size,Vector2 viewport) =>
        Vector2.Clamp(origin,Vector2.Zero,Vector2.Max(Vector2.Zero,viewport-size));

    internal static Vector2 PortraitCaptionPosition(NativeHuntRect bounds,NativeHuntRect fitted) =>
        new(bounds.Position.X,fitted.Position.Y+fitted.Size.Y+4);

    internal static NativeHuntRect PortraitCrop(ReadOnlySpan<byte> pixels,int width,int height) {
        var full=new NativeHuntRect(Vector2.Zero,new(width,height));
        if(width<=0||height<=0||(long)width*height*4>pixels.Length)return full;
        int left=width,top=height,right=-1,bottom=-1;
        for(int y=0;y<height;y++) {
            for(int x=0;x<width;x++) {
                if(pixels[(y*width+x)*4+3]==0)continue;
                left=Math.Min(left,x);
                right=Math.Max(right,x);
                top=Math.Min(top,y);
                bottom=Math.Max(bottom,y);
            }
        }
        return right<0?full:new(new(left,top),new(right-left+1,bottom-top+1));
    }

    public static TravelDialogLayout Build(TravelDialogContent content,Func<string,float,uint,float> measure,Func<string,float>? measureButton=null,Func<string,float>? measureTitleWidth=null) {
        const float left=18,top=38,portraitSize=192,portraitGap=14;
        float width=content.PortraitIcon!=0?DialogWidth+32:DialogWidth;
        float inner=width-2*left;
        NativeHuntRect? portrait=content.PortraitIcon!=0?new(new(width-left-portraitSize,top),new(portraitSize,portraitSize)):null;
        float textWidth=portrait.HasValue?inner-portraitSize-portraitGap:inner;
        var texts=new List<DialogText>();
        bool centerNotice=content.CenterNoticeText&&!portrait.HasValue;
        float Text(string value,float x,float y,float w,uint font,Vector4 color,bool centered=false) {
            float height=Math.Max(font+3,measure(value,w,font));
            if(!float.IsFinite(height) || height<0)throw new ArgumentException("Invalid text measurement");
            texts.Add(new(value,new(x,y),new(w,height),font,color,centered&&height<=font+8));return height;
        }
        bool useSideHeading=content.NextZone&&portrait.HasValue
            &&measureTitleWidth?.Invoke(content.Title)<=textWidth-8;
        int firstColumnText=0;
        float y=top;
        if(content.NextZone) {
            float headingWidth=useSideHeading?textWidth:inner;
            y+=Text(content.Eyebrow,left,y,headingWidth,NativeHuntLayout.SecondaryFont,Brand)+2;
            y+=Text(content.Title,left,y,headingWidth,NativeHuntLayout.TitleFont,White)+8;
            if(!useSideHeading&&portrait is {} nextImage) {
                portrait=nextImage with {Position=new(nextImage.Position.X,y)};
                firstColumnText=texts.Count;
            }
        } else {
            if(content.Eyebrow.Length>0)y+=Text(content.Eyebrow,left,y,textWidth,NativeHuntLayout.SecondaryFont,Brand,centerNotice)+2;
            y+=Text(content.Title,left,y,textWidth,NativeHuntLayout.TitleFont,White,centerNotice)+4;
        }
        bool hasSummary=content.Summary.Length>0;
        if(hasSummary)y+=Text(content.Summary,left,y,textWidth,NativeHuntLayout.TitleFont,Mint)+8;
        if(!string.IsNullOrEmpty(content.Destination)&&content.Destination!=content.Title) {
            string destination=string.IsNullOrEmpty(content.DestinationLabel)?content.Destination:$"{content.DestinationLabel} · {content.Destination}";
            y+=Text(destination,left,y,textWidth,NativeHuntLayout.BodyFont,White)+2;
        }
        if(!string.IsNullOrEmpty(content.Context)) y+=Text(content.Context,left,y,textWidth,NativeHuntLayout.SecondaryFont,hasSummary?Muted:Mint,centerNotice)+4;
        if(content.Fare.HasValue)y+=Text(content.FareLabel,left,y,textWidth,NativeHuntLayout.BodyFont,Mint)+4;
        bool captionUnderPortrait=content.NextZone&&portrait.HasValue&&content.Subject.Length>0;
        if(content.HasSubject) {
            y+=2;
            string subject=string.IsNullOrEmpty(content.SubjectLabel)?content.Subject:$"{content.SubjectLabel} · {content.Subject}";
            if(hasSummary&&content.Detail.Length>0)subject+=" · "+content.Detail;
            if(!captionUnderPortrait)y+=Text(subject,left,y,textWidth,NativeHuntLayout.BodyFont,Brand);
            if(!hasSummary&&!string.IsNullOrEmpty(content.Detail)) y+=4+Text(content.Detail,left,y+4,textWidth,NativeHuntLayout.SecondaryFont,Muted);
            y+=4;
        }
        if(portrait is {} image) {
            float below=image.Position.Y+image.Size.Y+4;
            if(captionUnderPortrait)below+=Text(content.Subject,image.Position.X,below,image.Size.X,NativeHuntLayout.BodyFont,Brand,centered:true)+4;
            if(content.NextZone&&y<below) {
                float offset=MathF.Floor((below-y)/2);
                int leftTextEnd=captionUnderPortrait?texts.Count-1:texts.Count;
                for(int i=firstColumnText;i<leftTextEnd;i++) {
                    var text=texts[i];
                    texts[i]=text with {Position=text.Position+new Vector2(0,offset)};
                }
            }
            y=Math.Max(y,below);
        }
        if(content.Metrics.Length>0) {
            float cardWidth=(inner-(content.Metrics.Length-1)*8)/content.Metrics.Length,rowHeight=0;
            for(int i=0;i<content.Metrics.Length;i++) {
                var metric=content.Metrics[i];float x=left+i*(cardWidth+8),my=y+4;
                my+=Text(metric.Label,x,my,cardWidth,NativeHuntLayout.SecondaryFont,Muted)+2;
                my+=Text(metric.Value,x,my,cardWidth,metric.Highlight?NativeHuntLayout.TitleFont:NativeHuntLayout.BodyFont,metric.Highlight?Mint:White)+4;
                rowHeight=Math.Max(rowHeight,my-y);
            }
            y+=rowHeight+4;
        }
        if(!string.IsNullOrEmpty(content.Footer)) y+=Text(content.Footer,left,y,inner,NativeHuntLayout.SecondaryFont,Muted)+4;
        const float buttonHeight=NativeHuntLayout.ActionHeight;
        float confirmWidth=Math.Max(NativeHuntLayout.ButtonWidth(content.ConfirmLabel),measureButton==null?0:MathF.Ceiling(measureButton(content.ConfirmLabel))+16);
        float declineWidth=Math.Max(NativeHuntLayout.ActionWidth,measureButton==null?0:MathF.Ceiling(measureButton(content.DeclineLabel))+16);
        float buttonsY=y+6,heightTotal=buttonsY+buttonHeight+12;
        float buttonsX=(width-confirmWidth-declineWidth-6)/2;
        var confirm=new DialogButton(new(buttonsX,buttonsY),new(confirmWidth,buttonHeight));
        var decline=new DialogButton(new(buttonsX+confirmWidth+6,buttonsY),new(declineWidth,buttonHeight));
        return new(width,heightTotal,confirm,decline,texts,portrait);
    }
}
