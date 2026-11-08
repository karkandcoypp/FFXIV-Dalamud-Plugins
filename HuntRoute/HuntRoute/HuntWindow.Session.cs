using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

public sealed partial class HuntWindow {
 internal static void DrawSession(Plugin p){
  bool next=p.Session.Phase==RoutePhase.NextZone&&p.Session.Suggested!=null;
  bool planned=p.Session.Phase==RoutePhase.AwaitingTravel;
  var target=next?p.Session.Suggested:p.Session.Target;
  if(!HuntTheme.BeginCard("session"))return;
  string status=p.Flow.Pauses!=PauseReason.None?"НА ПАУЗЕ":planned?"НУЖЕН ТЕЛЕПОРТ":next?"ЗОНА ЗАВЕРШЕНА":p.Session.Phase==RoutePhase.WaitingForZone?"ПЕРЕХОД В ЗОНУ":p.Flow.Manual!=null?"РУЧНАЯ ЦЕЛЬ":"ТЕКУЩАЯ ЦЕЛЬ";
  Colored(p.Flow.Pauses!=PauseReason.None?HuntTheme.Gold:HuntTheme.Muted,status);
  HuntTheme.Heading(target?.Name??"Выбираем цель");
  if(target!=null){
   MobPreview.OnHover(target,p.Kills(target));
   ImGui.TextWrapped(target.TerritoryName??"Зона охоты");
   if(!next){
    ImGui.TextUnformatted($"{p.Kills(target)} / {target.NeededKills}"+(target.IsEliteMark?" · недельная элита":""));
    ImGui.ProgressBar(target.NeededKills==0?0:(float)p.Kills(target)/target.NeededKills,new(-1,4),"");
   }
  }
  if(p.Flow.Pauses!=PauseReason.None){ImGui.TextWrapped(PauseLabel(p.Flow.Pauses));HuntTheme.Hint(p.Flow.Status);}
  else if(planned)ImGui.TextWrapped("Подтвердите телепорт к первой цели.");
  else if(!p.NavigationAllowed)ImGui.TextWrapped("Уточняем данные контракта…");
  else if(target is {IsEliteMark:true}&&!next){
   ImGui.TextWrapped(p.Search.AwaitingCredit?"Проверяем зачёт":p.Search.Lost?"Последняя позиция элиты":p.Search.ActorId!=null?"Элита обнаружена":$"Поиск · круг {Math.Max(1,p.Search.Round)}");
   HuntTheme.Hint(p.Search.Status);
  }
  else if(!next&&target!=null)ImGui.TextWrapped(p.LocationLabel);
  ImGui.Spacing();
  string action=p.Flow.Pauses!=PauseReason.None?p.Flow.Pauses.HasFlag(PauseReason.Marker)?"Вернуть метку":"Продолжить":next||planned?"Телепорт":"Пауза";
  float mapWidth=ImGui.CalcTextSize("Карта").X+18,moreWidth=ImGui.CalcTextSize("Ещё").X+18;
  float primaryWidth=Math.Max(60,ImGui.GetContentRegionAvail().X-mapWidth-moreWidth-16);
  bool stacked=ImGui.CalcTextSize(action).X+18>primaryWidth;
  if(stacked){primaryWidth=ImGui.GetContentRegionAvail().X;mapWidth=moreWidth=(primaryWidth-8)/2;}
  if(HuntTheme.Primary(action,new(primaryWidth,32))){if(p.Flow.Pauses!=PauseReason.None)p.ResumeJourney();else if(planned)p.OfferPlannedTravel();else if(next)p.OfferNext();else p.TogglePause();}
  if(!stacked)ImGui.SameLine();ImGui.BeginDisabled(target==null);
  if(ImGui.Button("Карта",new(mapWidth,32))&&target!=null)p.ShowMap(target);ImGui.EndDisabled();
  ImGui.SameLine();if(ImGui.Button("Ещё",new(moreWidth,32)))ImGui.OpenPopup("journey-actions");
  if(ImGui.BeginPopup("journey-actions")){
   if(planned&&ImGui.MenuItem("Пауза маршрута"))p.TogglePause();
   if(ImGui.MenuItem("К текущей цели"))p.FocusTarget();
   if(p.Flow.Manual!=null&&ImGui.MenuItem("Вернуться в авторежим")){p.Flow.Manual=null;p.ResumeJourney();}
   if(p.Search.Lost&&!p.Search.AwaitingCredit&&ImGui.MenuItem("Снять закрепление элиты"))p.ReleaseLostTarget();
   ImGui.Separator();if(ImGui.MenuItem("Остановить маршрут"))p.StopJourney();ImGui.EndPopup();
  }
  HuntTheme.EndCard();
 }
 private static string PauseLabel(PauseReason reasons)=>string.Join("; ",Enum.GetValues<PauseReason>().Where(r=>r!=PauseReason.None&&reasons.HasFlag(r)).Select(r=>r switch{
  PauseReason.User=>"Пауза по вашему выбору",PauseReason.Restored=>"Маршрут восстановлен",PauseReason.Marker=>"Вы изменили игровую метку",PauseReason.NoZone=>"Цель в другой зоне",PauseReason.TravelError=>"Прибытие не подтверждено",PauseReason.Unavailable=>"Цели временно недоступны",PauseReason.Board=>"Посещение доски",PauseReason.FlagError=>"Не удалось поставить метку",_=>"Данные обновляются"}));
}
