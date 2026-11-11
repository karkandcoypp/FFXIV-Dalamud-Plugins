using System;
using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

// Presentation and explicit commands. Building a screen never changes the hunt.
public sealed class NativeHuntController {
 private readonly Plugin plugin;
 public NativeHuntPage Page {get;private set;}
 public int Offset {get;private set;}
 public bool DeferredOnly {get;private set;}
 public bool OnlyNewBoards {get;private set;}
 private string? selected;
 private string? feedback;
 private string expansion="",zone="";
 private string numberKey="ordinary-distance";
 public NativeHuntController(Plugin plugin)=>this.plugin=plugin;
 public void Navigate(NativeHuntPage page){Page=page;Offset=0;OnlyNewBoards=false;}
 public void Boards(bool onlyNew){Navigate(NativeHuntPage.Boards);OnlyNewBoards=onlyNew;}
 public void FocusGoal(){selected=plugin.Session.Target is {} m?JourneyFlow.Key(m):null;Navigate(NativeHuntPage.Target);}
 public void Turn(int delta,int count)=>Offset=NativeHuntNavigation.ClampPage(Offset+delta,count);
 private static NativeHuntButton Button(string key,string label,string kind,string argument="",bool enabled=true,int delta=0)=>new(key,label,new(kind,argument,delta),enabled,kind!="pause"&&(key is "primary" or "track" or "start"));
 public NativeHuntScreen Build(bool mini=false){
  if(!plugin.Ready)return new("Hunt Route",plugin.Error??"Данные персонажа загружаются.",[Button("refresh","Обновить данные","refresh")]);
  var errors=new[]{plugin.JourneySaveError,plugin.SettingsSaveError,plugin.HistorySaveError}.Where(s=>s!=null).ToArray();
  var screen=mini?Hunt(true):Page switch {NativeHuntPage.Targets=>Targets(),NativeHuntPage.Boards=>BoardScreen(),NativeHuntPage.Settings=>Settings(),NativeHuntPage.Advanced=>Advanced(),NativeHuntPage.Target=>Target(),NativeHuntPage.Actions=>Actions(),NativeHuntPage.Filters=>Filters(),NativeHuntPage.Expansions=>Expansions(),NativeHuntPage.Zones=>Zones(),NativeHuntPage.Number=>Number(),_=>Hunt(false)};
  if(errors.Length>0)screen=screen with {Summary="Не удалось сохранить изменения.\n"+screen.Summary,Buttons=mini?screen.Buttons:new[]{Button("retry-save","Повторить сохранение","retry-save")}.Concat(screen.Buttons.Where(b=>b.Key!="retry-save")).ToArray()};
  return screen;
 }
 private NativeHuntScreen Hunt(bool mini){
  var buttons=new List<NativeHuntButton>();var target=plugin.Session.Phase==RoutePhase.NextZone?plugin.Session.Suggested:plugin.Session.Target;
  bool remaining=plugin.Mobs.Any(m=>Plugin.AllTiers.Contains(m.BillNumber)&&!plugin.Done(m)&&!plugin.Flow.Excluded(m));
  string title="Охота",summary;
  System.Numerics.Vector4? summaryColor=null;
  if(plugin.Flow.Active){
   title=target?.Name??"Маршрут охоты";
   string status=(plugin.Flow.Pauses.HasFlag(PauseReason.NoZone)?"Нет целей в этой зоне":plugin.Flow.Pauses!=PauseReason.None?plugin.Flow.Status:plugin.Session.Phase==RoutePhase.AwaitingTravel?"Подтвердите телепорт":plugin.Session.Phase==RoutePhase.NextZone?"Зона завершена":target is {IsEliteMark:true}?plugin.Search.AwaitingCredit?"Проверяем зачёт":plugin.Search.Lost?"Последняя позиция элиты":plugin.Search.ActorId!=null?"Элита обнаружена":$"Поиск элиты · круг {Math.Max(1,plugin.Search.Round)}":plugin.LocationLabel);
   summary=mini&&target!=null?MiniStatus():target==null?status:$"{target.TerritoryName} · {status}";
   summaryColor=plugin.Flow.Pauses!=PauseReason.None||plugin.Session.Phase is RoutePhase.AwaitingTravel or RoutePhase.NextZone or RoutePhase.WaitingForZone
    ?NativeHuntColors.Attention:NativeHuntColors.Tracking;
   string kind=plugin.Flow.Pauses!=PauseReason.None?"resume":plugin.Session.Phase==RoutePhase.AwaitingTravel?"planned":plugin.Session.Phase==RoutePhase.NextZone?"next":"pause";
   buttons.Add(Button("primary",kind is "planned" or "next"?"Телепорт":kind=="pause"?"Пауза":"Продолжить",kind));
   if(target!=null)buttons.Add(Button("map","Карта","map",JourneyFlow.Key(target)));
   buttons.Add(Button("actions","Маршрут","page",NativeHuntPage.Actions.ToString()));
  }else{
   int count=plugin.Mobs.Count(m=>Plugin.AllTiers.Contains(m.BillNumber)&&!plugin.Done(m));summary=count>0?$"Осталось целей: {count}":"Нет незавершённых целей.";
   buttons.Add(remaining?Button("primary","Начать охоту","resume"):Button("primary","Доски охоты","page",NativeHuntPage.Boards.ToString()));
  }
  if(!mini){buttons.Add(Button("targets","Все цели","page",NativeHuntPage.Targets.ToString()));if(feedback!=null)summary+="\n"+feedback;else if(plugin.NoticeText!=null)buttons.Add(Button("message","Сообщение охоты","message"));}
  return new(title,summary,buttons,target?.Icon??0,target==null?null:new(plugin.Kills(target),target.NeededKills),summaryColor);
 }
 private string MiniStatus(){
  var pauses=plugin.Flow.Pauses;
  if(pauses.HasFlag(PauseReason.Marker))return "Метка изменена";
  if(pauses.HasFlag(PauseReason.NoZone))return "Цель в другой зоне";
  if(pauses.HasFlag(PauseReason.TravelError))return "Телепорт не удался";
  if(pauses.HasFlag(PauseReason.FlagError))return "Метка недоступна";
  if(pauses.HasFlag(PauseReason.Unavailable))return "Цель недоступна";
  if(pauses.HasFlag(PauseReason.Board))return "Поездка к доске";
  if(pauses!=PauseReason.None)return "Маршрут на паузе";
  return plugin.Session.Phase switch{
   RoutePhase.AwaitingTravel or RoutePhase.NextZone=>"Нужен телепорт",
   RoutePhase.WaitingForZone=>"Ожидаем прибытия",
   _=>""
  };
 }
 private NativeHuntScreen Targets(){
  var goals=plugin.Routes.SelectMany(r=>r.Mobs).Where(m=>Plugin.AllTiers.Contains(m.BillNumber)&&MatchesExpansion(m)&&(zone==""||m.TerritoryName==zone)&&(!plugin.HideDone||!plugin.Done(m))&&(!DeferredOnly||plugin.Flow.Excluded(m)||plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m)))).DistinctBy(JourneyFlow.Key).ToArray();
  var buttons=new List<NativeHuntButton>{Button("filters","Фильтры","page",NativeHuntPage.Filters.ToString())};
  if(!plugin.Flow.Active&&goals.Any(m=>!plugin.Done(m)&&!plugin.Flow.Excluded(m)))buttons.Add(Button("start","Начать охоту","resume"));
  if(plugin.Flow.Active&&plugin.Session.Target!=null)buttons.Add(Button("focus","К цели","focus"));
  foreach(var group in goals.GroupBy(goal=>goal.TerritoryName??"Зона неизвестна"))
  foreach(var goal in group){
   bool deferred=plugin.Flow.Excluded(goal);
   string name=(goal.Name??"Цель")+(goal.IsEliteMark?" [B]":"");
   var row=new NativeGoalRow(group.Key,name,$"{plugin.Kills(goal)}/{goal.NeededKills}"+(deferred?" · позже":""));
   buttons.Add(Button("goal:"+JourneyFlow.Key(goal),name,"target",JourneyFlow.Key(goal)) with {
    Goal=row,LabelColor=plugin.Done(goal)?NativeHuntColors.Ready:deferred?NativeHuntColors.Attention:null});
  }
  return new("Цели маршрута",$"Целей: {goals.Length}",buttons);
 }
 private bool MatchesExpansion(MobHuntEntry m)=>expansion==""||Plugin.Boards.Any(b=>b.Name==expansion&&(b.Daily.Contains(m.BillNumber)||b.Weekly==m.BillNumber));
 private NativeHuntScreen Filters()=>new("Фильтры целей","",[
  Button("expansion","Дополнение: "+(expansion==""?"Все":expansion),"page",NativeHuntPage.Expansions.ToString()),Button("zone","Зона: "+(zone==""?"Все":zone),"page",NativeHuntPage.Zones.ToString()),
  Button("done-filter",plugin.HideDone?"Показать выполненные":"Скрыть выполненные","filter-done"),Button("deferred-filter",DeferredOnly?"Показать все цели":"Отложенные / недоступные","filter-deferred"),Button("reset-filters","Сбросить фильтры","filter-reset"),Button("targets","К целям","page",NativeHuntPage.Targets.ToString())]);
 private NativeHuntScreen Expansions()=>new("Дополнение","Выберите дополнение для списка целей.",new[]{Button("all-expansions","Все дополнения","filter-expansion")}.Concat(Plugin.Boards.Select(b=>Button("expansion:"+b.Name,b.Name,"filter-expansion",b.Name))).ToArray());
 private static NativeHuntButton ZoneRow(string key,string label,string zone)=>Button(key,label,"filter-zone",zone) with {Goal=new("",label,"")};
 private NativeHuntScreen Zones()=>new("Зона","Выберите зону для списка целей.",new[]{ZoneRow("all-zones","Все зоны","")}.Concat(plugin.Mobs.Where(MatchesExpansion).Select(m=>m.TerritoryName).OfType<string>().Distinct().Order(StringComparer.Ordinal).Select(name=>ZoneRow("zone:"+name,name,name))).ToArray());
 private NativeHuntScreen Target(){
  var goal=plugin.Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==selected);
  if(goal==null)return new("Цель недоступна","Контракт изменился или завершён.",[Button("targets","К списку целей","page",NativeHuntPage.Targets.ToString())]);
  bool excluded=plugin.Flow.Excluded(goal);string summary=goal.TerritoryName??"";
  if(plugin.Flow.Unavailable.TryGetValue(JourneyFlow.Key(goal),out var reason))summary+="\n"+reason;
  if(excluded)summary+="\nОтложено";
  var buttons=new List<NativeHuntButton>{Button("track","Сопровождать","track",selected!,!excluded&&!plugin.Done(goal)),Button("map","Карта","map",selected!),Button("defer",excluded?"Вернуть в маршрут":"Отложить цель",excluded?"restore":"defer",selected!)};
  if(!excluded)buttons.Add(Button("defer-zone","Отложить зону","defer-zone",selected!));
  buttons.Add(Button("targets","К списку целей","page",NativeHuntPage.Targets.ToString()));return new(goal.Name??"Цель",summary,buttons,goal.Icon,new(plugin.Kills(goal),goal.NeededKills));
 }
 private NativeHuntScreen BoardScreen(){
  var buttons=Plugin.Boards.Select(b=>{
   var row=new NativeBoardRow(b.Name,Progress(b,false),Progress(b,true));
   return Button("board:"+b.Name,row.Name,"board",b.Name,plugin.BoardReachable(b)&&(!OnlyNewBoards||plugin.CanPickup(b,false)||plugin.CanPickup(b,true))) with {Board=row};
  }).ToList();
  buttons.Add(Button("boards-all",OnlyNewBoards?"Показать все доски":"Обновить",OnlyNewBoards?"boards-all":"refresh"));
  var now=DateTimeOffset.UtcNow;
  var day=ResetClock.Next(now,false)-now;
  var week=ResetClock.Next(now,true)-now;
  string weekly=week.Days>0?$"{week.Days} д {week.Hours} ч":$"{week.Hours} ч {week.Minutes} мин";
  return new("Доски охоты","",buttons,BoardTimers:new($"{(int)day.TotalHours} ч {day.Minutes} мин",weekly));
 }
 private NativeBoardStatus Progress(Board b,bool weekly){
  if(plugin.CompletionUncertain(b,weekly))return new("неизвестно");
  var p=plugin.CollectionProgress(b,weekly);
  if(p.AllCompleted)return new("выполнено",NativeHuntColors.Ready);
  if(p.Total==0)return new("не открыто");
  if(p.Taken>0)return new($"{p.Taken}/{p.Total} взято",NativeHuntColors.Tracking);
  if(plugin.CanPickup(b,weekly))return new("можно взять",NativeHuntColors.Attention);
  return new(p.Completed>0?$"{p.Completed}/{p.Total} готово":"нет новых");
 }
 private NativeHuntScreen Settings(){
  var s=plugin.Settings;
  NativeHuntButton ToggleRow(string key,string label,bool value)=>
   Button(key,label,"toggle",key,!plugin.PersistenceBlocked) with {Checked=value};
  return new("Настройки","",[
   ToggleRow("mini","Минипанель",s.Mini),ToggleRow("reset","Охота при входе и сбросе",s.ResetNotifications),
   ToggleRow("elite","Метки элиты",s.WorldMarkers),ToggleRow("ordinary","Метки обычных мобов",s.OrdinaryWorldMarkers),
   ToggleRow("elite-sound","Звук обнаружения элиты",s.EliteSound),ToggleRow("next-sound","Звук перехода в зону",s.NextZoneSound)]);
 }
 private NativeHuntScreen Advanced(){
  var s=plugin.Settings;var buttons=new List<NativeHuntButton>{Button("shortcuts",$"Телепорт в зоне: {(s.Shortcuts?"Вкл":"Выкл")}","toggle","shortcuts",!plugin.PersistenceBlocked)};
  foreach(string key in new[]{"elite-distance","ordinary-distance","gil","seconds","percent"}){var (label,value)=NumberValue(key);buttons.Add(Button("number:"+key,$"{label}: {value}","edit-number",key,!plugin.PersistenceBlocked));}
  buttons.Add(Button("refresh","Обновить данные","refresh"));buttons.Add(Button("retry-save","Повторить сохранение","retry-save"));buttons.Add(Button("export","Сохранить диагностику","export"));buttons.Add(Button("classic","Резервное окно мыши","classic"));
  return new("Дополнительные параметры",feedback??"Лимит 0 гил разрешает только бесплатные телепорты.",buttons);
 }
 private (string Label,string Value) NumberValue(string key){var s=plugin.Settings;return key switch{"elite-distance"=>("Метки элиты",s.WorldDistance+" м"),"ordinary-distance"=>("Дальность меток",s.OrdinaryWorldDistance+" м"),"gil"=>("Лимит гил",s.MaxGil.ToString()),"seconds"=>("Экономия времени",$"{s.MinSeconds:F1} с"),"percent"=>("Экономия пути",s.MinPercent+" %"),_=>("Параметр","")};}
 private NativeHuntScreen Number(){var (label,value)=NumberValue(numberKey);return new(label,$"{value}",new[]{-1,-10,-50,1,10,50}.Select(d=>Button("step:"+d,(d<0?"-":"+")+(numberKey=="seconds"?$"{Math.Abs(d)/10f:F1} с":Math.Abs(d).ToString()),"step",numberKey,!plugin.PersistenceBlocked,d)).ToArray());}
 private NativeHuntScreen Actions(){
  var buttons=new List<NativeHuntButton>();
  if(plugin.Flow.Active){
   string travel=plugin.Flow.Pauses==PauseReason.None?plugin.Session.Phase switch{
    RoutePhase.AwaitingTravel=>"planned",RoutePhase.NextZone=>"next",_=>""
   }:"";
   if(travel.Length>0)buttons.Add(Button("primary","Телепорт",travel));
   else if(plugin.Flow.Pauses!=PauseReason.None)buttons.Add(Button("resume",plugin.Flow.Pauses==PauseReason.Marker?"Вернуть метку":"Продолжить маршрут","resume"));
   else if(plugin.Session.Phase!=RoutePhase.WaitingForZone)buttons.Add(Button("resume","Вернуть метку","resume"));
   if(!plugin.Flow.Pauses.HasFlag(PauseReason.User))buttons.Add(Button("pause","Пауза маршрута","pause"));
   buttons.Add(Button("stop","Остановить","stop"));
  }
  if(plugin.Flow.Manual!=null)buttons.Add(Button("auto","Вернуться в авторежим","auto"));
  if(plugin.Search.Lost&&!plugin.Search.AwaitingCredit)buttons.Add(Button("release","Снять закрепление элиты","release"));
  if(plugin.Session.Target!=null)buttons.Add(Button("focus","К цели","focus"));
  buttons.Add(Button("targets","Все цели","page",NativeHuntPage.Targets.ToString()));string status=plugin.Flow.Active&&plugin.Flow.Pauses==PauseReason.None
   ?plugin.Session.Phase switch {RoutePhase.AwaitingTravel or RoutePhase.NextZone=>"Ожидается подтверждение перехода",RoutePhase.WaitingForZone=>"Ожидается прибытие",_=>"Сопровождение активно"}
   :plugin.Flow.Status;
  return new("Действия маршрута",status,buttons);
 }
 public void Execute(NativeHuntAction action){
  var goal=plugin.Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==action.Argument);
  switch(action.Kind){
   case "page":if(Enum.TryParse<NativeHuntPage>(action.Argument,out var page))Navigate(page);break;
   case "target":if(goal!=null){selected=action.Argument;plugin.Selected=selected;Navigate(NativeHuntPage.Target);}break;
   case "focus":FocusGoal();break;
   case "resume":plugin.ResumeJourney();feedback=plugin.NoticeText??plugin.Flow.Status;Navigate(NativeHuntPage.Hunt);break;
   case "pause":if(!plugin.Flow.Pauses.HasFlag(PauseReason.User))plugin.TogglePause();break;case "stop":plugin.StopJourney();Navigate(NativeHuntPage.Hunt);break;
   case "planned":plugin.OfferPlannedTravel();break;case "next":plugin.OfferNext();break;
   case "map":if(goal!=null)plugin.ShowMap(goal);break;case "track":if(goal!=null){plugin.Track(goal);Navigate(NativeHuntPage.Hunt);}break;
   case "defer":if(goal!=null)plugin.Defer(goal);break;case "defer-zone":if(goal!=null)plugin.Defer(goal,true);break;case "restore":if(goal!=null)plugin.RestoreDeferred(goal);break;
   case "auto":plugin.Flow.Manual=null;plugin.ResumeJourney();Navigate(NativeHuntPage.Hunt);break;case "release":plugin.ReleaseLostTarget();break;
   case "board":var board=Plugin.Boards.FirstOrDefault(b=>b.Name==action.Argument);if(board!=null&&plugin.BoardReachable(board)&&(!OnlyNewBoards||plugin.CanPickup(board,false)||plugin.CanPickup(board,true))){plugin.PickupOpen=OnlyNewBoards;plugin.OfferBoard(board);plugin.PickupOpen=false;}break;
   case "boards-all":OnlyNewBoards=false;break;case "filter-done":plugin.HideDone=!plugin.HideDone;Offset=0;break;case "filter-deferred":DeferredOnly=!DeferredOnly;Offset=0;break;
   case "filter-expansion":expansion=action.Argument;zone="";Navigate(NativeHuntPage.Filters);break;
   case "filter-zone":zone=action.Argument;Navigate(NativeHuntPage.Filters);break;
   case "filter-reset":zone=expansion="";DeferredOnly=false;plugin.HideDone=true;Offset=0;break;
   case "toggle":Toggle(action.Argument);break;case "step":Step(action.Argument,action.Delta);break;
   case "edit-number":if(!plugin.PersistenceBlocked&&NumberValue(action.Argument).Value.Length>0){numberKey=action.Argument;Navigate(NativeHuntPage.Number);}break;
   case "refresh":plugin.Refresh();break;case "retry-save":plugin.RetryPersistence();feedback=plugin.NoticeText;break;
   case "export":plugin.Export();feedback=plugin.NoticeText;break;case "message":feedback=plugin.NoticeText;Navigate(NativeHuntPage.Actions);break;
   case "classic":plugin.OpenClassic();break;
  }
 }
 private void Toggle(string key){if(plugin.PersistenceBlocked)return;var s=plugin.Settings;switch(key){case "mini":plugin.SetMini(!s.Mini);return;case "reset":s.ResetNotifications=!s.ResetNotifications;break;case "elite":s.WorldMarkers=!s.WorldMarkers;break;case "ordinary":s.OrdinaryWorldMarkers=!s.OrdinaryWorldMarkers;break;case "elite-sound":s.EliteSound=!s.EliteSound;break;case "next-sound":s.NextZoneSound=!s.NextZoneSound;break;case "shortcuts":s.Shortcuts=!s.Shortcuts;break;default:return;}plugin.SaveJourney(true);}
 private void Step(string key,int delta){if(plugin.PersistenceBlocked)return;var s=plugin.Settings;switch(key){case "elite-distance":s.WorldDistance=Math.Clamp(s.WorldDistance+delta,50,500);break;case "ordinary-distance":s.OrdinaryWorldDistance=Math.Clamp(s.OrdinaryWorldDistance+delta,50,500);break;case "gil":s.MaxGil=(uint)Math.Clamp((long)s.MaxGil+delta,0,uint.MaxValue);break;case "seconds":s.MinSeconds=Math.Max(0,s.MinSeconds+delta/10f);break;case "percent":s.MinPercent=Math.Clamp(s.MinPercent+delta,0,100);break;default:return;}s.Validate();plugin.SaveJourney(true);}
}
