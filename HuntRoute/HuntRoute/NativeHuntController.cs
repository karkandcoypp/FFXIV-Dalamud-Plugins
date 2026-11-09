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
 private static NativeHuntButton Button(string key,string label,string kind,string argument="",bool enabled=true,int delta=0)=>new(key,label,new(kind,argument,delta),enabled,key is "primary" or "track" or "start");
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
  if(plugin.Flow.Active){
   title=target?.Name??"Маршрут охоты";
   string status=(plugin.Flow.Pauses!=PauseReason.None?plugin.Flow.Status:plugin.Session.Phase==RoutePhase.AwaitingTravel?"Подтвердите телепорт":plugin.Session.Phase==RoutePhase.NextZone?"Зона завершена":target is {IsEliteMark:true}?plugin.Search.AwaitingCredit?"Проверяем зачёт":plugin.Search.Lost?"Последняя позиция элиты":plugin.Search.ActorId!=null?"Элита обнаружена":$"Поиск элиты · круг {Math.Max(1,plugin.Search.Round)}":plugin.LocationLabel);
   summary=target==null?status:$"{target.TerritoryName} · {(target.IsEliteMark?"Недельная цель":"Ежедневная цель")}\n{status}";
   string kind=plugin.Flow.Pauses!=PauseReason.None?"resume":plugin.Session.Phase==RoutePhase.AwaitingTravel?"planned":plugin.Session.Phase==RoutePhase.NextZone?"next":"pause";
   buttons.Add(Button("primary",kind is "planned" or "next"?"Телепортироваться":kind=="pause"?"Пауза":"Продолжить охоту",kind));
   if(target!=null)buttons.Add(Button("map","Карта","map",JourneyFlow.Key(target)));
   buttons.Add(Button("actions","Маршрут","page",NativeHuntPage.Actions.ToString()));
  }else{
   int count=plugin.Mobs.Count(m=>Plugin.AllTiers.Contains(m.BillNumber)&&!plugin.Done(m));summary=count>0?$"Незавершённых целей: {count}\nВыберите «Начать охоту» или верните отложенные цели.":"Нет взятых незавершённых целей.\nДоступность и выполнение — в разделе «Доски».";
   buttons.Add(remaining?Button("primary","Начать охоту","resume"):Button("primary","Доски охоты","page",NativeHuntPage.Boards.ToString()));
  }
  if(!mini){buttons.Add(Button("targets","Все цели","page",NativeHuntPage.Targets.ToString()));if(feedback!=null)summary+="\n"+feedback;else if(plugin.NoticeText!=null)buttons.Add(Button("message","Сообщение охоты","message"));}
  return new(title,summary,buttons,target?.Icon??0,target==null?null:new(plugin.Kills(target),target.NeededKills));
 }
 private NativeHuntScreen Targets(){
  var goals=plugin.Routes.SelectMany(r=>r.Mobs).Where(m=>Plugin.AllTiers.Contains(m.BillNumber)&&MatchesExpansion(m)&&(zone==""||m.TerritoryName==zone)&&(!plugin.HideDone||!plugin.Done(m))&&(!DeferredOnly||plugin.Flow.Excluded(m)||plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m)))).DistinctBy(JourneyFlow.Key).ToArray();
  var buttons=new List<NativeHuntButton>{Button("filters","Фильтры целей","page",NativeHuntPage.Filters.ToString())};
  if(!plugin.Flow.Active&&goals.Any(m=>!plugin.Done(m)&&!plugin.Flow.Excluded(m)))buttons.Add(Button("start","Начать охоту","resume"));
  if(plugin.Flow.Active&&plugin.Session.Target!=null)buttons.Add(Button("focus","К текущей цели","focus"));
  foreach(var goal in goals)buttons.Add(Button("goal:"+JourneyFlow.Key(goal),$"{goal.Name}"+(goal.IsEliteMark?" [B]":"")+$"\n{goal.TerritoryName} · {plugin.Kills(goal)}/{goal.NeededKills}"+(plugin.Flow.Excluded(goal)?" · отложено":""),"target",JourneyFlow.Key(goal)));
  return new("Цели маршрута",$"Целей в списке: {goals.Length}. Выберите цель для карты, портрета и сопровождения.",buttons);
 }
 private bool MatchesExpansion(MobHuntEntry m)=>expansion==""||Plugin.Boards.Any(b=>b.Name==expansion&&(b.Daily.Contains(m.BillNumber)||b.Weekly==m.BillNumber));
 private NativeHuntScreen Filters()=>new("Фильтры целей","Фильтры меняют только список, не маршрут.",[
  Button("expansion","Дополнение: "+(expansion==""?"Все":expansion),"page",NativeHuntPage.Expansions.ToString()),Button("zone","Зона: "+(zone==""?"Все":zone),"page",NativeHuntPage.Zones.ToString()),
  Button("done-filter",plugin.HideDone?"Показать выполненные":"Скрыть выполненные","filter-done"),Button("deferred-filter",DeferredOnly?"Показать все цели":"Только отложенные / недоступные","filter-deferred"),Button("reset-filters","Сбросить фильтры","filter-reset"),Button("targets","К целям","page",NativeHuntPage.Targets.ToString())]);
 private NativeHuntScreen Expansions()=>new("Дополнение","Выберите дополнение для списка целей.",new[]{Button("all-expansions","Все дополнения","filter-expansion")}.Concat(Plugin.Boards.Select(b=>Button("expansion:"+b.Name,b.Name,"filter-expansion",b.Name))).ToArray());
 private NativeHuntScreen Zones()=>new("Зона","Выберите зону для списка целей.",new[]{Button("all-zones","Все зоны","filter-zone")}.Concat(plugin.Mobs.Where(MatchesExpansion).Select(m=>m.TerritoryName).OfType<string>().Distinct().Order(StringComparer.Ordinal).Select(name=>Button("zone:"+name,name,"filter-zone",name))).ToArray());
 private NativeHuntScreen Target(){
  var goal=plugin.Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==selected);
  if(goal==null)return new("Цель недоступна","Контракт изменился или завершён.",[Button("targets","К списку целей","page",NativeHuntPage.Targets.ToString())]);
  bool excluded=plugin.Flow.Excluded(goal);string summary=goal.TerritoryName??"";
  if(plugin.Flow.Unavailable.TryGetValue(JourneyFlow.Key(goal),out var reason))summary+="\n"+reason;
  if(excluded)summary+="\nОтложено; выполненным не считается.";
  var buttons=new List<NativeHuntButton>{Button("track","Сопровождать эту цель","track",selected!,!excluded&&!plugin.Done(goal)),Button("map","Карта","map",selected!),Button("defer",excluded?"Вернуть в маршрут":"Отложить цель",excluded?"restore":"defer",selected!)};
  if(!excluded)buttons.Add(Button("defer-zone","Отложить зону","defer-zone",selected!));
  buttons.Add(Button("targets","К списку целей","page",NativeHuntPage.Targets.ToString()));return new(goal.Name??"Цель",summary,buttons,goal.Icon,new(plugin.Kills(goal),goal.NeededKills));
 }
 private NativeHuntScreen BoardScreen(){
  var buttons=Plugin.Boards.Select(b=>Button("board:"+b.Name,$"{b.Name} · день: {Progress(b,false)} · неделя: {Progress(b,true)}","board",b.Name,plugin.BoardReachable(b)&&(!OnlyNewBoards||plugin.CanPickup(b,false)||plugin.CanPickup(b,true)))).ToList();
  buttons.Add(Button("boards-all",OnlyNewBoards?"Показать все доски":"Обновить статусы",OnlyNewBoards?"boards-all":"refresh"));
  var now=DateTimeOffset.UtcNow;var day=ResetClock.Next(now,false)-now;var week=ResetClock.Next(now,true)-now;return new("Доски охоты",$"Выберите дополнение. Поездка требует подтверждения.\nСброс дня через {(int)day.TotalHours} ч {day.Minutes} мин · недели через {(int)week.TotalDays} д {week.Hours} ч",buttons);
 }
 private string Progress(Board b,bool weekly){if(plugin.CompletionUncertain(b,weekly))return "история неизвестна";var p=plugin.CollectionProgress(b,weekly);return p.AllCompleted?"выполнено":p.Total==0?"не открыто":p.Taken>0?$"{p.Taken}/{p.Total} взято":plugin.CanPickup(b,weekly)?"можно взять":p.Completed>0?$"{p.Completed}/{p.Total} выполнено":"нет новых";}
 private NativeHuntScreen Settings(){
  var s=plugin.Settings;string On(bool v)=>v?"Вкл":"Выкл";
  return new("Настройки","Выберите пункт для переключения. Дополнительные параметры — на следующей странице.",[
   Button("mini",$"Минипанель: {On(s.Mini)}","toggle","mini",!plugin.PersistenceBlocked),Button("reset",$"Охота при входе и сбросе: {On(s.ResetNotifications)}","toggle","reset",!plugin.PersistenceBlocked),Button("elite",$"Подсветка элиты: {On(s.WorldMarkers)}","toggle","elite",!plugin.PersistenceBlocked),Button("ordinary",$"Подсветка обычных целей: {On(s.OrdinaryWorldMarkers)}","toggle","ordinary",!plugin.PersistenceBlocked),
   Button("elite-sound",$"Звук элиты: {On(s.EliteSound)}","toggle","elite-sound",!plugin.PersistenceBlocked),Button("next-sound",$"Звук следующей зоны: {On(s.NextZoneSound)}","toggle","next-sound",!plugin.PersistenceBlocked),Button("advanced","Дальности, телепорты и обслуживание","page",NativeHuntPage.Advanced.ToString())]);
 }
 private NativeHuntScreen Advanced(){
  var s=plugin.Settings;var buttons=new List<NativeHuntButton>{Button("shortcuts",$"Сокращение перелётов: {(s.Shortcuts?"Вкл":"Выкл")}","toggle","shortcuts",!plugin.PersistenceBlocked)};
  foreach(string key in new[]{"elite-distance","ordinary-distance","gil","seconds","percent"}){var (label,value)=NumberValue(key);buttons.Add(Button("number:"+key,$"{label}: {value}","edit-number",key,!plugin.PersistenceBlocked));}
  buttons.Add(Button("refresh","Обновить данные","refresh"));buttons.Add(Button("retry-save","Повторить сохранение","retry-save"));buttons.Add(Button("export","Сохранить диагностику локально","export"));buttons.Add(Button("classic","Резервное окно мыши","classic"));
  return new("Дополнительные параметры",feedback??"Выберите значение для настройки. Лимит гил 0 — только бесплатные телепорты.",buttons);
 }
 private (string Label,string Value) NumberValue(string key){var s=plugin.Settings;return key switch{"elite-distance"=>("Дальность элиты",s.WorldDistance+" м"),"ordinary-distance"=>("Дальность обычных целей",s.OrdinaryWorldDistance+" м"),"gil"=>("Лимит гил",s.MaxGil.ToString()),"seconds"=>("Экономия времени",$"{s.MinSeconds:F1} с"),"percent"=>("Экономия пути",s.MinPercent+" %"),_=>("Параметр","")};}
 private NativeHuntScreen Number(){var (label,value)=NumberValue(numberKey);return new(label,$"Текущее значение: {value}\nИзменения сохраняются сразу.",new[]{-50,-10,-1,1,10,50}.Select(d=>Button("step:"+d,(d<0?"Уменьшить на ":"Увеличить на ")+(numberKey=="seconds"?$"{Math.Abs(d)/10f:F1} с":Math.Abs(d).ToString()),"step",numberKey,!plugin.PersistenceBlocked,d)).ToArray());}
 private NativeHuntScreen Actions(){
  var buttons=new List<NativeHuntButton>();
  if(plugin.Flow.Active){buttons.Add(Button("resume","Продолжить / вернуть метку","resume"));buttons.Add(Button("pause","Пауза маршрута","pause"));buttons.Add(Button("stop","Остановить маршрут","stop"));}
  if(plugin.Flow.Manual!=null)buttons.Add(Button("auto","Вернуться в авторежим","auto"));
  if(plugin.Search.Lost&&!plugin.Search.AwaitingCredit)buttons.Add(Button("release","Снять закрепление элиты","release"));
  if(plugin.Session.Target!=null)buttons.Add(Button("focus","К текущей цели","focus"));
  buttons.Add(Button("targets","Все цели","page",NativeHuntPage.Targets.ToString()));return new("Действия маршрута",feedback??plugin.Flow.Status,buttons);
 }
 public void Execute(NativeHuntAction action){
  var goal=plugin.Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==action.Argument);
  switch(action.Kind){
   case "page":if(Enum.TryParse<NativeHuntPage>(action.Argument,out var page))Navigate(page);break;
   case "target":if(goal!=null){selected=action.Argument;plugin.Selected=selected;Navigate(NativeHuntPage.Target);}break;
   case "focus":FocusGoal();break;
   case "resume":plugin.ResumeJourney();feedback=plugin.NoticeText??plugin.Flow.Status;Navigate(NativeHuntPage.Hunt);break;
   case "pause":plugin.TogglePause();break;case "stop":plugin.StopJourney();Navigate(NativeHuntPage.Hunt);break;
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
