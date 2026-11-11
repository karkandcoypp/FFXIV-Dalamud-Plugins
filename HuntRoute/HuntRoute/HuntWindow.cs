using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace HuntRoute;

public enum HuntPage { Hunt,Targets,Boards,Settings }

public sealed partial class HuntWindow:Window {
 private readonly Plugin plugin;
 private HuntPage page;
 private int expansion=-1;
 private string zone="";
 private bool onlyNewBoards;
 private bool revealSelected;
 private string? startFeedback;
 private void StartHunt(){
  page=HuntPage.Hunt;string? previous=plugin.NoticeText;plugin.ResumeJourney();
  startFeedback=!plugin.Flow.Active?plugin.Flow.Status:plugin.NoticeText!=previous?plugin.NoticeText:null;
 }
 public HuntWindow(Plugin plugin):base("Hunt Route###HuntRoute"){
  this.plugin=plugin;Size=new(480,540);SizeCondition=ImGuiCond.FirstUseEver;
  Flags=ImGuiWindowFlags.AlwaysVerticalScrollbar;
  SizeConstraints=new(){MinimumSize=new(400,340),MaximumSize=new(float.MaxValue,float.MaxValue)};
 }
 public override void PreDraw()=>HuntTheme.Push();
 public override void PostDraw()=>HuntTheme.Pop();
 public override void Draw(){
  // Explicit requests only: automatic target changes never navigate these views.
  if(plugin.FocusRequested){page=HuntPage.Targets;expansion=-1;zone="";plugin.HideDone=false;plugin.DeferredOpen=false;revealSelected=true;plugin.FocusRequested=false;}
  if(plugin.PickupOpen){page=HuntPage.Boards;onlyNewBoards=true;plugin.PickupOpen=false;}
  if(plugin.SettingsOpen){page=HuntPage.Settings;plugin.SettingsOpen=false;}
  DrawNavigation();DrawPersistenceWarning();
  if(!plugin.Ready){HuntTheme.Heading("Подключаемся к охоте");ImGui.TextWrapped(plugin.Error??"Данные персонажа загружаются.");if(ImGui.Button("Обновить"))plugin.Refresh();return;}
  switch(page){case HuntPage.Hunt:DrawHunt();break;case HuntPage.Targets:DrawTargets();break;case HuntPage.Boards:DrawBoards();break;case HuntPage.Settings:DrawSettings();break;}
 }
 private void DrawNavigation(){
  string[] labels=["Охота","Доски","Настройки"];HuntPage[] pages=[HuntPage.Hunt,HuntPage.Boards,HuntPage.Settings];
  float width=(ImGui.GetContentRegionAvail().X-16)/3;
  if(labels.Any(label=>ImGui.CalcTextSize(label).X+18>width)){
   ImGui.SetNextItemWidth(-1);
   if(ImGui.BeginCombo("##navigation",page==HuntPage.Targets?"Охота / все цели":labels[Array.IndexOf(pages,page)])){
    for(int i=0;i<labels.Length;i++)if(ImGui.Selectable(labels[i],page==pages[i])){page=pages[i];onlyNewBoards=false;}
    ImGui.EndCombo();
   }
   ImGui.Spacing();return;
  }
  for(int i=0;i<labels.Length;i++){
   if(i>0)ImGui.SameLine();bool selected=page==pages[i]||i==0&&page==HuntPage.Targets;
   ImGui.PushStyleColor(ImGuiCol.Button,selected?HuntTheme.Selected:HuntTheme.Surface);
   ImGui.PushStyleColor(ImGuiCol.Text,selected?HuntTheme.Text:HuntTheme.Muted);
   if(ImGui.Button(labels[i],new(width,34))){page=pages[i];onlyNewBoards=false;}
   ImGui.PopStyleColor(2);
  }
  ImGui.Spacing();
 }
 private void DrawPersistenceWarning(){
  var errors=new[]{plugin.JourneySaveError,plugin.SettingsSaveError,plugin.HistorySaveError}.Where(s=>s!=null).ToArray();
  if(errors.Length==0)return;
  Colored(HuntTheme.Gold,"Изменения пока не сохранены");HuntTheme.Hint(string.Join("\n",errors));
  if(ImGui.SmallButton("Повторить сохранение"))plugin.RetryPersistence();
 }
 private void DrawHunt(){
  var remaining=plugin.Mobs.Where(m=>Plugin.AllTiers.Contains(m.BillNumber)&&!plugin.Done(m)).ToArray();
  bool active=plugin.Flow.Active;
  if(active)DrawSession(plugin);
  else{
   if(!HuntTheme.BeginCard("ready"))return;
   int available=remaining.Count(m=>!plugin.Flow.Excluded(m));
   bool pickup=Plugin.Boards.Any(b=>plugin.CanPickup(b,false)||plugin.CanPickup(b,true));
   bool completed=Plugin.Boards.Any(b=>plugin.CollectionProgress(b,false).AllCompleted||plugin.CollectionProgress(b,true).AllCompleted);
   HuntTheme.Heading(available>0?"Готовы к охоте?":remaining.Length>0?"Цели отложены":pickup?"Новые контракты":completed?"Взятые цели выполнены":"Нет взятых контрактов");
   ImGui.TextWrapped(available>0?$"Осталось целей: {available} · зон: {remaining.Select(m=>m.TerritoryType).Distinct().Count()}":remaining.Length>0?$"Незавершённых целей: {remaining.Length}":pickup?"Выберите дополнение на вкладке «Доски».":"Статусы и время сброса — на вкладке «Доски».");
   ImGui.Spacing();
   if(available>0&&HuntTheme.Primary("Начать охоту",new(-1,36)))StartHunt();
   HuntTheme.EndCard();
  }
  if(startFeedback!=null){ImGui.TextWrapped(startFeedback);if(ImGui.SmallButton("Скрыть пояснение"))startFeedback=null;}
  ImGui.Spacing();if(ImGui.Button($"Все цели · {remaining.Length}",new(-1,32)))page=HuntPage.Targets;
  if(!active&&remaining.Length>0){
   var first=plugin.Routes.SelectMany(r=>r.Mobs).FirstOrDefault(m=>!plugin.Done(m)&&!plugin.Flow.Excluded(m));
   if(first!=null){ImGui.Spacing();ImGui.TextDisabled("ПЕРВАЯ ЗОНА");ImGui.TextWrapped(first.TerritoryName??"Зона охоты");}
  }
  if(plugin.NoticeText!=null&&ImGui.CollapsingHeader("Сообщение")){ImGui.TextWrapped(plugin.NoticeText);if(ImGui.SmallButton("Прочитано"))plugin.NoticeText=null;}
 }
 private void DrawBoards(){
  HuntTheme.Heading("Доски охоты");var now=DateTimeOffset.UtcNow;
  ImGui.TextWrapped("Ежедневный сброс через "+Countdown(ResetClock.Next(now,false)-now)); ImGui.TextWrapped("Еженедельный сброс через "+Countdown(ResetClock.Next(now,true)-now));
  if(onlyNewBoards){ImGui.TextDisabled("Выберите доступные новые контракты");if(ImGui.SmallButton("Показать все доски"))onlyNewBoards=false;}
  foreach(var board in Plugin.Boards){
   ImGui.PushID(board.Name);if(!HuntTheme.BeginCard("board")){ImGui.PopID();continue;}
   float buttonWidth=ImGui.CalcTextSize("К доске").X+20;float titleWidth=ImGui.GetContentRegionAvail().X-buttonWidth-8;
   float buttonX=ImGui.GetCursorPosX()+ImGui.GetContentRegionAvail().X-buttonWidth;
   ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+titleWidth);ImGui.TextWrapped(board.Name);ImGui.PopTextWrapPos();
   ImGui.SameLine(buttonX);
   bool available=plugin.CanPickup(board,false)||plugin.CanPickup(board,true);bool reachable=plugin.BoardReachable(board);
   ImGui.BeginDisabled(!reachable||onlyNewBoards&&!available);
   if(ImGui.Button("К доске")){plugin.PickupOpen=onlyNewBoards;plugin.OfferBoard(board);plugin.PickupOpen=false;}
   ImGui.EndDisabled();HuntTheme.Hint(!reachable?"Эфирит города не открыт.":onlyNewBoards&&!available?"Новых доступных контрактов нет.":"Подтвердить поездку к доске. В своём городе — только метка.");
   Progress(board,false);Progress(board,true);HuntTheme.EndCard();ImGui.PopID();
  }
 }
 private void Progress(Board board,bool weekly){
  string prefix=weekly?"Еженедельный: ":"Ежедневные: ";
  if(plugin.CompletionUncertain(board,weekly)){Colored(HuntTheme.Gold,prefix+"история не подтверждена");return;}
  var p=plugin.CollectionProgress(board,weekly);
  string label=p.AllCompleted?"выполнено":p.Total==0?"не открыто":p.Taken>0?$"взято {p.Taken}/{p.Total}":plugin.CanPickup(board,weekly)?"можно взять":p.Completed>0?$"выполнено {p.Completed}/{p.Total}":"нет доступных";
  Colored(p.AllCompleted?HuntTheme.Accent:p.Taken>0?HuntTheme.Text:HuntTheme.Muted,!weekly&&p.AllCompleted?"Выполнено сегодня":prefix+label);
  HuntTheme.Hint(p.Label+$". Взято {p.Taken}/{p.Total}, выполнено {p.Completed}/{p.Total}.");
 }
 internal static void Colored(Vector4 color,string text){ImGui.PushStyleColor(ImGuiCol.Text,color);ImGui.TextWrapped(text);ImGui.PopStyleColor();}
 private static string Countdown(TimeSpan time)=>time.TotalDays>=1?$"{(int)time.TotalDays} д {time.Hours} ч":$"{(int)time.TotalHours} ч {time.Minutes:00} мин";
}

public sealed class MiniWindow:Window {
 private readonly Plugin plugin;
 public MiniWindow(Plugin plugin):base("Охота###HuntRouteMini"){this.plugin=plugin;Size=new(340,190);SizeCondition=ImGuiCond.FirstUseEver;Flags=ImGuiWindowFlags.AlwaysAutoResize;SizeConstraints=new(){MinimumSize=new(300,120),MaximumSize=new(500,900)};IsOpen=true;RespectCloseHotkey=false;}
 public override void OnClose()=>plugin.SetMini(false);
 public override bool DrawConditions()=>plugin.NativeMain?.Available!=true&&plugin.Ready&&plugin.Settings.Mini&&plugin.Flow.Active;
 public override void PreDraw()=>HuntTheme.Push();
 public override void PostDraw()=>HuntTheme.Pop();
 public override void Draw()=>HuntWindow.DrawSession(plugin);
}
