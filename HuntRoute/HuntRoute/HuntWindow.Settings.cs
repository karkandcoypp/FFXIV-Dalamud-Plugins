using System;
using Dalamud.Bindings.ImGui;
namespace HuntRoute;

public sealed partial class HuntWindow {
 private string? diagnosticNotice;
 private void DrawSettings(){
  HuntTheme.Heading("Настройки");
  ImGui.BeginDisabled(plugin.PersistenceBlocked);
  var s=plugin.Settings;bool mini=s.Mini,elite=s.EliteSound,next=s.NextZoneSound,reset=s.ResetNotifications,shortcuts=s.Shortcuts,world=s.WorldMarkers,ordinary=s.OrdinaryWorldMarkers;
  int distance=s.WorldDistance,ordinaryDistance=s.OrdinaryWorldDistance,gil=(int)Math.Min(int.MaxValue,s.MaxGil);float seconds=s.MinSeconds,percent=s.MinPercent;
  bool dirty=ImGui.Checkbox("Минипанель сопровождения",ref mini);
  dirty|=ImGui.Checkbox("Предлагать охоту при входе",ref reset);HuntTheme.Hint("Также уведомлять о новых контрактах после сброса.");
  dirty|=ImGui.Checkbox("Подсветка элиты в мире",ref world);
  dirty|=ImGui.Checkbox("Подсветка обычных целей",ref ordinary);HuntTheme.Hint("Только живые нужные мобы из взятых контрактов. Текущий экземпляр выделяется ярче.");
  ImGui.Spacing();
  if(ImGui.CollapsingHeader("Звуки")){
   dirty|=ImGui.Checkbox("Элита обнаружена",ref elite);dirty|=ImGui.Checkbox("Готова следующая зона",ref next);
  }
  if(ImGui.CollapsingHeader("Телепорты внутри зоны")){
   dirty|=ImGui.Checkbox("Предлагать сокращение пути",ref shortcuts);
   ImGui.BeginDisabled(!shortcuts);
   ImGui.TextUnformatted("Лимит гил · 0 — бесплатно");ImGui.SetNextItemWidth(-1);dirty|=ImGui.InputInt("##gil",ref gil);
   ImGui.TextUnformatted("Минимум экономии, секунд");ImGui.SetNextItemWidth(-1);dirty|=ImGui.InputFloat("##seconds",ref seconds);
   ImGui.TextUnformatted("Минимум экономии, %");ImGui.SetNextItemWidth(-1);dirty|=ImGui.SliderFloat("##percent",ref percent,0,100);ImGui.EndDisabled();
  }
  if((world||ordinary)&&ImGui.CollapsingHeader("Дальность подсветки")){
   if(world){ImGui.TextUnformatted("Элита");ImGui.SetNextItemWidth(-1);dirty|=ImGui.SliderInt("##distance",ref distance,50,500,"%d м");}
   if(ordinary){ImGui.TextUnformatted("Обычные цели");ImGui.SetNextItemWidth(-1);dirty|=ImGui.SliderInt("##ordinary-distance",ref ordinaryDistance,50,500,"%d м");}
  }
  if(dirty){
   bool miniChanged=s.Mini!=mini;s.EliteSound=elite;s.NextZoneSound=next;s.ResetNotifications=reset;s.Shortcuts=shortcuts;s.WorldMarkers=world;s.WorldDistance=distance;s.OrdinaryWorldMarkers=ordinary;s.OrdinaryWorldDistance=ordinaryDistance;s.MaxGil=(uint)Math.Max(0,gil);s.MinSeconds=seconds;s.MinPercent=percent;s.Validate();
   if(miniChanged)plugin.SetMini(mini);else plugin.SaveJourney(true);
  }
  ImGui.EndDisabled();
  if(ImGui.CollapsingHeader("Обслуживание")){
   if(ImGui.Button("Обновить данные"))plugin.Refresh();if(ImGui.Button("Сохранить диагностику")){plugin.Export();diagnosticNotice=plugin.NoticeText;}
   HuntTheme.Hint("Только локальный файл; данные никуда не отправляются.");
   if(diagnosticNotice!=null)ImGui.TextWrapped(diagnosticNotice);
  }
 }
}
