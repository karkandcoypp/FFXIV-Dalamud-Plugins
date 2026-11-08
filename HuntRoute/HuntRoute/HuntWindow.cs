using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace HuntRoute;
public sealed class HuntWindow:Window {
 private readonly Plugin plugin;
 private int expansion=-1;
 private string zone="";
 public HuntWindow(Plugin plugin):base("Hunt Route###HuntRoute"){this.plugin=plugin;Size=new(800,620);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(520,430),MaximumSize=new(float.MaxValue,float.MaxValue)};}
 public override void PreDraw()=>HuntTheme.Push();
 public override void PostDraw()=>HuntTheme.Pop();
 public override void Draw(){
  if(plugin.FocusRequested){expansion=-1;zone="";plugin.FocusRequested=false;}
  Colored(HuntTheme.Brand,"HUNT ROUTE / Охота по контрактам");
  foreach(var failure in new[]{plugin.JourneySaveError,plugin.SettingsSaveError,plugin.HistorySaveError}.Where(s=>s!=null))ImGui.TextWrapped(failure!);
  if(plugin.JourneySaveError!=null||plugin.SettingsSaveError!=null||plugin.HistorySaveError!=null){if(ImGui.Button("Повторить сохранение"))plugin.RetryPersistence();}
  if(!plugin.Ready){var loading=EmptyView.Describe(false,0,0,0,0,0);ImGui.TextWrapped(plugin.Error??loading.Text);if(ImGui.Button(loading.Action))plugin.Refresh();return;}
  bool canStart=plugin.Flow.Active||plugin.Mobs.Any(m=>Plugin.AllTiers.Contains(m.BillNumber)&&!plugin.Done(m)&&!plugin.Flow.Excluded(m));
  ImGui.BeginDisabled(!canStart);
  if(ImGui.Button(plugin.Flow.Active?"Продолжить охоту":"Начать охоту"))plugin.ResumeJourney();
  ImGui.EndDisabled();HuntTheme.Hint(canStart?"Запустить общий маршрут. Поездка в другую зону требует подтверждения.":"Нет взятых доступных целей. Выберите доску или верните отложенные цели.");
  ImGui.SameLine();if(ImGui.Button("Доски охоты"))plugin.PickupOpen=!plugin.PickupOpen;
  ImGui.SameLine();if(ImGui.Button("Настройки"))plugin.SettingsOpen=!plugin.SettingsOpen;
  if(ImGui.SmallButton("Обновить"))plugin.Refresh();
  DrawSession(plugin);
  if(plugin.NoticeText is {} notice){ImGui.TextWrapped(notice);if(ImGui.SmallButton("Скрыть сообщение"))plugin.NoticeText=null;}
  if(plugin.SettingsOpen)DrawSettings();
  if(ImGui.CollapsingHeader("Сводка / все пять дополнений",ImGuiTreeNodeFlags.DefaultOpen)){
  if(ImGui.BeginTable("overview",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)){
   ImGui.TableSetupColumn("Дополнение",ImGuiTableColumnFlags.WidthStretch);
   ImGui.TableSetupColumn("Ежедневные",ImGuiTableColumnFlags.WidthStretch);
   ImGui.TableSetupColumn("Недельный",ImGuiTableColumnFlags.WidthStretch);
   ImGui.TableSetupColumn("Доска",ImGuiTableColumnFlags.WidthFixed,70);
   ImGui.TableHeadersRow();
   foreach(var b in Plugin.Boards){ImGui.PushID(b.Name);ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextWrapped(b.Name);
    ImGui.TableNextColumn();Progress(b,false);ImGui.TableNextColumn();Progress(b,true);ImGui.TableNextColumn();
    bool available=plugin.CanPickup(b,false)||plugin.CanPickup(b,true);
    bool reachable=plugin.BoardReachable(b);ImGui.BeginDisabled(!reachable||plugin.PickupOpen&&!available);if(ImGui.SmallButton("К доске"))plugin.OfferBoard(b);ImGui.EndDisabled();
    if(!reachable)HuntTheme.Hint("Эфирит города не открыт; если добраться в город самостоятельно, доска будет доступна без телепорта.");
    else if(plugin.PickupOpen&&!available)HuntTheme.Hint("Нет подтверждённых доступных новых контрактов. Статусы показаны слева.");
    ImGui.PopID();}
   ImGui.EndTable();
  }
  var now=DateTimeOffset.UtcNow;
  ImGui.TextWrapped("Сброс: ежедневные "+Countdown(ResetClock.Next(now,false)-now)+" / недельный "+Countdown(ResetClock.Next(now,true)-now));
  }
  ImGui.Separator();
  ImGui.SetNextItemWidth(Math.Min(220,ImGui.GetContentRegionAvail().X));
  if(ImGui.BeginCombo("##view",expansion<0?"Просмотр: все дополнения":Plugin.Boards[expansion].Name)){
   if(ImGui.Selectable("Все дополнения",expansion<0))expansion=-1;
   for(int i=0;i<Plugin.Boards.Length;i++)if(ImGui.Selectable(Plugin.Boards[i].Name,expansion==i))expansion=i;
   ImGui.EndCombo();
  }
  ImGui.SameLine();ImGui.Checkbox("Скрыть готовые",ref plugin.HideDone);
  if(ImGui.SmallButton("Отложенные"))plugin.DeferredOpen=!plugin.DeferredOpen;
  var source=plugin.Routes.SelectMany(r=>r.Mobs).Where(m=>Plugin.AllTiers.Contains(m.BillNumber)&&(expansion<0||Plugin.Boards[expansion].Daily.Contains(m.BillNumber)||Plugin.Boards[expansion].Weekly==m.BillNumber)).ToArray();
  var zones=source.Select(m=>m.TerritoryName??"Зона").Distinct().ToArray();
  ImGui.SetNextItemWidth(-1);
  if(ImGui.BeginCombo("##zones",zone==""?"Просмотр: все зоны":zone)){
   if(ImGui.Selectable("Все зоны",zone==""))zone="";
   foreach(var name in zones)if(ImGui.Selectable(name,zone==name))zone=name;
   ImGui.EndCombo();
  }
  var shown=source.Where(m=>(zone==""||m.TerritoryName==zone)&&(!plugin.HideDone||!plugin.Done(m))&&(!plugin.DeferredOpen||plugin.Flow.Excluded(m)||plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m)))).ToArray();
  var all=plugin.Mobs.Where(m=>Plugin.AllTiers.Contains(m.BillNumber)).ToArray();var remaining=all.Where(m=>!plugin.Done(m)).ToArray();
  if(shown.Length==0||remaining.Length>0&&remaining.All(m=>plugin.Flow.Excluded(m)||plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m)))){
   bool completed=Plugin.Boards.Any(b=>plugin.CollectionProgress(b,false).AllCompleted||plugin.CollectionProgress(b,true).AllCompleted);
   var empty=all.Length==0&&completed?EmptyView.Describe(true,1,0,0,0,0):EmptyView.Describe(true,all.Length,remaining.Length,remaining.Count(plugin.Flow.Excluded),remaining.Count(m=>!plugin.Flow.Excluded(m)&&plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m))),0);
   ImGui.TextWrapped(empty.Text);if(ImGui.Button(empty.Action)){
    if(empty.Kind is EmptyKind.NoBills or EmptyKind.Completed)plugin.PickupOpen=true;
    else{expansion=-1;zone="";plugin.HideDone=false;plugin.DeferredOpen=empty.Kind is EmptyKind.Deferred or EmptyKind.Unavailable;if(empty.Kind==EmptyKind.Unavailable)plugin.Refresh();}
   }
  }
  var selected=plugin.Mobs.FirstOrDefault(m=>JourneyFlow.Key(m)==plugin.Selected);
  if(selected!=null){
   Colored(HuntTheme.Brand,"Выбрано: "+selected.Name);ImGui.TextWrapped(selected.TerritoryName??"");
   bool excluded=plugin.Flow.Excluded(selected);ImGui.BeginDisabled(plugin.Done(selected)||excluded);
   if(ImGui.Button("Сопровождать эту цель"))plugin.Track(selected);ImGui.EndDisabled();ImGui.SameLine();
   if(ImGui.Button("Карта"))plugin.ShowMap(selected);
   if(excluded){ImGui.SameLine();if(ImGui.Button("Вернуть в маршрут"))plugin.RestoreDeferred(selected);}
   else{if(ImGui.Button("Отложить"))plugin.Defer(selected);ImGui.SameLine();if(ImGui.Button("Отложить зону"))plugin.Defer(selected,true);}
   if(plugin.Flow.Unavailable.TryGetValue(JourneyFlow.Key(selected),out var reason))ImGui.TextWrapped("Недоступно: "+reason);
  }
  if(ImGui.BeginTable("targets",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingStretchProp)){
   ImGui.TableSetupColumn("Цель",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Счёт",ImGuiTableColumnFlags.WidthFixed,48);ImGui.TableSetupColumn("Статус",ImGuiTableColumnFlags.WidthFixed,90);
   int step=0; (uint,uint)? group=null;
   foreach(var m in shown){if(group!=(m.TerritoryType,m.MapId)){group=(m.TerritoryType,m.MapId);ImGui.TableNextRow();ImGui.TableNextColumn();Colored(HuntTheme.Brand,m.TerritoryName??"Зона");}
    ImGui.PushID(JourneyFlow.Key(m));ImGui.TableNextRow();
    if(plugin.Session.Target is {} current&&JourneyFlow.Key(current)==JourneyFlow.Key(m))ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0,ImGui.GetColorU32(new Vector4(.16f,.28f,.34f,1)));
    ImGui.TableNextColumn();ImGui.TextWrapped($"{++step:00}  "+(m.IsEliteMark?"[B] ":"")+m.Name);if(ImGui.IsItemClicked())plugin.Selected=JourneyFlow.Key(m);MobPreview.OnHover(m,plugin.Kills(m));
    ImGui.TableNextColumn();ImGui.TextUnformatted($"{plugin.Kills(m)}/{m.NeededKills}");ImGui.TableNextColumn();
    ImGui.TextWrapped(plugin.Done(m)?"Готово":plugin.Flow.Excluded(m)?"Отложено":plugin.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m))?"Недоступно":"В маршруте");
    ImGui.PopID();}
   ImGui.EndTable();
  }
 }
 private void Progress(Board b,bool weekly){if(plugin.CompletionUncertain(b,weekly)){ImGui.TextWrapped("История не подтверждена");HuntTheme.Hint("Повреждённая история сохранена отдельно. Счётчики игры читаются, но выполнение за текущий период пока не подтверждено.");return;}var p=plugin.CollectionProgress(b,weekly);string text=p.AllCompleted?(weekly?"Неделя завершена":"Выполнено сегодня"):p.Label;Colored(p.AllCompleted?HuntTheme.Accent:p.Taken>0?HuntTheme.Brand:HuntTheme.Muted,text);HuntTheme.Hint($"Взято {p.Taken}/{p.Total}; выполнено {p.Completed}/{p.Total}. "+(plugin.CanPickup(b,weekly)?"Можно взять новые контракты.":"Зачёт сохраняется отдельно для персонажа до сброса."));}
 internal static void DrawSession(Plugin p){
  if(!p.Flow.Active&&p.Session.Phase==RoutePhase.Idle)return;
  var target=p.Session.Target;
  Colored(HuntTheme.Brand,p.Flow.Pauses!=PauseReason.None?"Сопровождение на паузе: "+PauseLabel(p.Flow.Pauses):p.Session.Phase==RoutePhase.Finished?"Доступные цели маршрута завершены":"Текущая цель: "+(target?.Name??"выбирается"));
  if(target!=null)MobPreview.OnHover(target,p.Kills(target));
  if(target!=null)ImGui.TextDisabled(p.LocationLabel);
  if(target!=null)ImGui.TextWrapped($"{target.TerritoryName} · {p.Kills(target)}/{target.NeededKills}");
  if(p.Flow.Pauses!=PauseReason.None||!p.NavigationAllowed||p.Session.Phase==RoutePhase.WaitingForZone)ImGui.TextWrapped(p.Flow.Status);
  if(ImGui.SmallButton(p.Flow.Pauses.HasFlag(PauseReason.User)?"Продолжить":"Пауза"))p.TogglePause();
  ImGui.SameLine();if(ImGui.SmallButton("Стоп"))p.StopJourney();
  if(ImGui.SmallButton("К текущей цели")&&target!=null)p.FocusTarget();
  ImGui.SameLine();if(ImGui.SmallButton("Карта")&&target!=null)p.ShowMap(target);
  if(p.Flow.Pauses!=PauseReason.None && p.Flow.Pauses!=PauseReason.User){if(ImGui.Button("Вернуть сопровождение"))p.ResumeJourney();}
  if(p.Flow.Manual!=null){ImGui.TextDisabled("Ручная цель имеет приоритет");if(ImGui.SmallButton("Вернуться в авторежим")){p.Flow.Manual=null;p.ResumeJourney();}}
  if(p.Session.Phase==RoutePhase.NextZone&&p.Session.Suggested is {} next){ImGui.TextWrapped("Следующая зона: "+next.TerritoryName);if(ImGui.Button("Предложить телепорт"))p.OfferNext();}
  if(target is {IsEliteMark:true}){ImGui.TextWrapped(p.Search.Status);if(p.Search.Lost&&!p.Search.AwaitingCredit&&ImGui.Button("Снять закрепление и продолжить поиск")){p.ReleaseLostTarget();}}
  int deferred=p.Mobs.Count(m=>!p.Done(m)&&(p.Flow.Excluded(m)||p.Flow.Unavailable.ContainsKey(JourneyFlow.Key(m))));if(deferred>0)ImGui.TextWrapped($"Незавершённых отложенных/недоступных целей: {deferred}");
 }
 private void DrawSettings(){
  ImGui.BeginDisabled(plugin.PersistenceBlocked);
  var s=plugin.Settings;bool mini=s.Mini,eliteSound=s.EliteSound,nextSound=s.NextZoneSound,reset=s.ResetNotifications,shortcuts=s.Shortcuts;
  bool world=s.WorldMarkers;int distance=s.WorldDistance;
  int gil=(int)Math.Min(int.MaxValue,s.MaxGil);float seconds=s.MinSeconds,percent=s.MinPercent;
  bool dirty=ImGui.Checkbox("Минипанель сопровождения",ref mini);dirty|=ImGui.Checkbox("Звук обнаружения элиты",ref eliteSound);dirty|=ImGui.Checkbox("Звук следующей зоны",ref nextSound);dirty|=ImGui.Checkbox("Предлагать новые контракты после сброса",ref reset);dirty|=ImGui.Checkbox("Предлагать сокращение перелёта",ref shortcuts);
  dirty|=ImGui.Checkbox("Подсветка элиты в игровом мире",ref world);
  ImGui.SetNextItemWidth(150);dirty|=ImGui.SliderInt("Дальность подсветки, м",ref distance,50,500);
  ImGui.SetNextItemWidth(150);dirty|=ImGui.InputInt("Лимит гил (0 = бесплатно)",ref gil);
  ImGui.SetNextItemWidth(150);dirty|=ImGui.InputFloat("Минимум экономии, с",ref seconds);
  ImGui.SetNextItemWidth(150);dirty|=ImGui.SliderFloat("Минимум экономии, %",ref percent,0,100);
  if(dirty){plugin.SetMini(mini);s.EliteSound=eliteSound;s.NextZoneSound=nextSound;s.ResetNotifications=reset;s.Shortcuts=shortcuts;s.WorldMarkers=world;s.WorldDistance=distance;s.MaxGil=(uint)Math.Max(0,gil);s.MinSeconds=seconds;s.MinPercent=percent;s.Validate();plugin.SaveJourney(true);}
  ImGui.EndDisabled();
  if(ImGui.Button("Сохранить диагностику локально"))plugin.Export();ImGui.Separator();
 }
 private static string PauseLabel(PauseReason reasons)=>string.Join("; ",Enum.GetValues<PauseReason>().Where(r=>r!=PauseReason.None&&reasons.HasFlag(r)).Select(r=>r switch{PauseReason.User=>"по вашему выбору",PauseReason.Restored=>"восстановленное сохранение",PauseReason.Marker=>"изменена игровая метка",PauseReason.NoZone=>"нет подходящей цели в этой зоне",PauseReason.TravelError=>"телепорт не подтверждён",PauseReason.Unavailable=>"цели недоступны",PauseReason.Board=>"посещение доски",PauseReason.FlagError=>"не удалось поставить метку",_=>"данные обновляются"}));
 internal static void Colored(Vector4 color,string text){ImGui.PushStyleColor(ImGuiCol.Text,color);ImGui.TextWrapped(text);ImGui.PopStyleColor();}
 private static string Countdown(TimeSpan t)=>t.TotalDays>=1?$"{(int)t.TotalDays} д {t.Hours} ч":$"{(int)t.TotalHours} ч {t.Minutes:00} мин";
}
public sealed class MiniWindow:Window {
 private readonly Plugin plugin;
 public MiniWindow(Plugin plugin):base("Охота###HuntRouteMini"){this.plugin=plugin;Size=new(360,180);SizeCondition=ImGuiCond.FirstUseEver;Flags=ImGuiWindowFlags.AlwaysAutoResize;SizeConstraints=new(){MinimumSize=new(300,120),MaximumSize=new(500,900)};IsOpen=true;RespectCloseHotkey=false;}
 public override void OnClose(){plugin.SetMini(false);}
 public override bool DrawConditions()=>plugin.Ready&&plugin.Settings.Mini&&plugin.Flow.Active;
 public override void PreDraw()=>HuntTheme.Push();
 public override void PostDraw()=>HuntTheme.Pop();
 public override void Draw()=>HuntWindow.DrawSession(plugin);
}
