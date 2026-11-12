using System;
using Dalamud.Game.ClientState.Conditions;
namespace HuntRoute;

internal sealed class NativeHuntUi:IDisposable {
 private readonly Plugin plugin;
 private readonly NativeDialogService dialogs;
 internal readonly NativeHuntController Controller;
 private NativeHuntAddon? main,mini;
 private NativeHuntAddon? lastFocused;
 private bool wanted,disposed,unavailable,miniAutoClose,hadModal;
 internal NativeHuntUi(Plugin plugin,NativeDialogService dialogs){this.plugin=plugin;this.dialogs=dialogs;Controller=new(plugin);}
 internal bool Available=>!disposed&&!unavailable&&dialogs.Initialized;
 internal void Open(NativeHuntPage? page=null){if(disposed)return;if(unavailable||dialogs.InitializationFailed){plugin.OpenClassic();return;}plugin.HideClassic();if(page is {} p)Controller.Navigate(p);wanted=true;main?.RequestFocus();}
 internal void Toggle(){if(wanted)CloseMain();else Open();}
 internal void CloseMain(){wanted=false;CloseSafely(main);}
 internal void FocusTarget(){Controller.FocusGoal();Open();}
 internal void CloseAll(){wanted=false;miniAutoClose=true;lastFocused=null;hadModal=false;Controller.Navigate(NativeHuntPage.Hunt);CloseSafely(main);CloseSafely(mini);}
 private static void CloseSafely(NativeHuntAddon? addon){try{addon?.Close();}catch(Exception e){Service.PluginLog.Debug(e,"Нативное окно не закрылось штатно");}}
 private bool OwnModal()=>dialogs.HasDialog||!Service.GameGui.GetAddonByName("HuntRouteConfirm").IsNull;
 private bool Modal()=>OwnModal()||!Service.GameGui.GetAddonByName("SelectYesno").IsNull||Service.Condition.Any(ConditionFlag.BetweenAreas,ConditionFlag.BetweenAreas51,ConditionFlag.WatchingCutscene,ConditionFlag.OccupiedInCutSceneEvent);
 internal void Pump(){
  if(disposed||unavailable)return;
  if(!dialogs.Initialized){if(wanted&&dialogs.InitializationFailed){wanted=false;plugin.NoticeText="Нативный интерфейс не загрузился. Открыто резервное окно.";plugin.OpenClassic();}return;}
  try{
   if(plugin.PickupOpen){Controller.Boards(true);plugin.PickupOpen=false;Open();}
   if(plugin.SettingsOpen){plugin.SettingsOpen=false;Open(NativeHuntPage.Settings);}
   if(plugin.FocusRequested){plugin.FocusRequested=false;FocusTarget();}
   if(!Service.ClientState.IsLoggedIn)return;
   bool showMini=plugin.Ready&&plugin.Settings.Mini&&plugin.Flow.Active;
   if(!wanted&&!showMini&&main?.IsOpen!=true&&mini?.IsOpen!=true){lastFocused=null;hadModal=false;return;}
   bool blocked=Modal();
   if(wanted&&!blocked){
    main??=new(Controller,Execute,()=>wanted=false,Modal){InternalName="HuntRouteMain",Title="Hunt Route",Size=new(640,660),EnableContextMenu=false,DisableClamping=false};
    if(!main.IsOpen&&main.AddonId==0)main.Open();
   }
   if(showMini&&!blocked){
    mini??=new(Controller,ExecuteMini,()=>{if(!miniAutoClose)plugin.SetMini(false);},Modal,true){InternalName="HuntRouteMiniNative",Title="Hunt Route",Size=new(460,390),EnableContextMenu=false,DisableClamping=false};
    if(!mini.IsOpen&&mini.AddonId==0){miniAutoClose=false;mini.Open();}
   }else if(!showMini&&mini?.IsOpen==true){miniAutoClose=true;mini.Close();}
   bool modal=OwnModal();
   if(hadModal&&!modal&&lastFocused?.IsOpen==true)lastFocused.RequestFocus();
   if(!modal)lastFocused=main?.OwnsFocus()==true?main:mini?.OwnsFocus()==true?mini:null;
   hadModal=modal;
  }catch(Exception e){unavailable=true;CloseAll();Service.PluginLog.Error(e,"Нативное окно Hunt Route недоступно");plugin.NoticeText="Нативное окно открыть не удалось. Открыто резервное окно мыши.";plugin.OpenClassic();}
 }
 private void Execute(NativeHuntAction action){Controller.Execute(action);if(action.Kind is "page" or "target" or "toggle" or "step" or "edit-number" or "focus" or "filter-done" or "filter-deferred" or "filter-expansion" or "filter-zone" or "filter-reset" or "boards-all")main?.RequestFocus();if(action.Kind=="classic")CloseMain();}
 private void ExecuteMini(NativeHuntAction action){if(action.Kind=="page"){Controller.Execute(action);Open();}else Controller.Execute(action);}
 public void Dispose(){disposed=true;wanted=false;miniAutoClose=true;main?.Dispose();mini?.Dispose();main=null;mini=null;}
}
