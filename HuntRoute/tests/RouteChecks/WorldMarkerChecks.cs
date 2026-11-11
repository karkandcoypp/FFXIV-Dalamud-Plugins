using System.Numerics;
using System.Text.Json;
using HuntRoute;
static class WorldMarkerChecks {
 public static void Run(Action<bool,string> check){
  var settings=new HuntSettings();var ordinary=new WorldHuntGoal("daily",1,"Mourner",10,20,false,1,3);
  var elite=new WorldHuntGoal("weekly",2,"Elite",10,20,true,0,1);
  WorldHuntActor[] actors=[new(10,1,new(10,0,0),false),new(11,1,new(20,0,0),false),new(12,2,new(30,0,0),false),new(13,99,new(1,0,0),false),new(14,1,new(1,0,0),true)];
  WorldHuntMarker[] Build(WorldHuntGoal[]? goals=null,WorldHuntActor[]? npcs=null,string? key=null,ulong? id=null)=>WorldHuntMarkers.Build(goals??[ordinary,elite],npcs??actors,Vector3.Zero,10,20,settings,key,id);
  check(Build().Length==3,"Only alive required ordinary/elite instances are labelled");
  var selected=Build(key:"daily",id:11);check(selected[0].ActorId==11&&selected.Count(m=>m.Current)==1&&selected[0].Radius>selected.Single(m=>m.ActorId==10).Radius,"Exactly the tracked ordinary instance is emphasized, even when another is closer");
  var sound=new EliteMarkerSound();
  var shown=selected.Single(m=>m.Elite);
  check(!shown.Current&&sound.Observe(shown,true,10,20),"A rendered elite alerts even with an ordinary target selected");
  check(!sound.Observe(shown,true,10,20)&&!sound.Observe(shown with {Current=true},true,10,20),"Repeated frames and retargeting stay silent");
  check(Build(npcs:[actors[0]]).All(m=>!m.Elite)&&!sound.Observe(shown,true,10,20),"A missing frame does not rearm a previously shown elite");
  check(!sound.Observe(selected[0],true,10,20),"Ordinary marks are silent");
  var newElite=shown with {ActorId=90};
  check(!sound.Observe(newElite,false,10,20)&&sound.Observe(newElite,true,10,20),"Muted marks do not play or consume a later enabled alert");
  check(sound.Observe(shown,true,11,21)&&!sound.Observe(shown,true,10,20),"Different maps keep separate alerts without rearming the original map");
  sound.Reset();check(sound.Observe(shown,true,10,20),"A new character session can receive its own alert");
  uint[] colors=[0xff62d24d,0xff81ecad,0xff63eaff,0xff539eff,0xff6666ff];
  float[] distances=[0,15,15.1f,35,35.1f,65,65.1f,100,100.1f];
  int[] bands=[0,0,1,1,2,2,3,3,4];
  var nearby=actors[0];
  var marker=selected[0];
  for(int i=0;i<distances.Length;i++){
   uint expected=colors[bands[i]];
   check((marker with {Distance=distances[i]}).Color==expected,"Distance marker uses the correct band at both sides of each boundary");
  }
  check((marker with {Current=false,Elite=true}).Color==marker.Color,"Elite and current styling do not override distance color");
  var far=actors[1] with {Position=new(110,0,0)};
  check(Build(npcs:[far]).Single().Color==colors[4]&&Build(npcs:[nearby,far]).Single(m=>m.ActorId==far.Id).Color==colors[4],"A far target stays red when other mobs enter or leave range");
  WorldHuntActor[] pack=[new(90,1,new(30,0,0),false),new(91,1,new(5,0,0),false),new(92,1,new(10,0,0),false),new(93,1,new(10,0,0),false),actors[2]];
  check(Build(npcs:pack,key:"daily",id:90).Where(m=>!m.Elite).Select(m=>m.ActorId).SequenceEqual(new ulong[]{91,92}),"Two remaining kills show the two nearest actors, even when a farther actor is tracked");
  check(Build([ordinary with {Kills=2},elite],pack).Where(m=>!m.Elite).Single().ActorId==91,"After a credited kill only the nearest ordinary actor stays marked");
  check(Build(npcs:pack).Any(m=>m.Elite),"Ordinary quota does not hide weekly elite markers");
  check(selected[0].Label.Contains("1/3")&&selected[0].Label.Contains("20 м")&&selected[0].Label.StartsWith("Цель"),"Ordinary label contains real count, distance and textual active-goal distinction");
  check(Build(key:"daily").Single(m=>m.Current).ActorId==10,"Before navigator instance selection only nearest current-type actor is emphasized");
  check(!Build(key:"daily",id:999).Any(m=>m.Current),"A known missing/off-range tracked instance never falsely highlights another actor");
  check(Build([ordinary with {Kills=3},elite]).All(m=>m.Elite),"Ordinary highlight disappears immediately once count is complete");
  check(Build([ordinary with {Known=false},elite]).All(m=>m.Elite),"Uncertain bill snapshots do not create ordinary labels");
  check(Build([ordinary with {Excluded=true},elite]).All(m=>m.Elite),"Deferred goals are not labelled as required");
  check(Build([ordinary with {Map=21},elite]).All(m=>m.Elite)&&Build([ordinary with {Territory=11},elite]).All(m=>m.Elite),"Ordinary labels require both actual territory and map match");
  var second=ordinary with {Key="second",Kills=0,Needed=2};
  check(Build([ordinary,second]).Length==2&&Build([ordinary,second],key:"second",id:10).First().Needed==2,"Duplicate contract species emit one label per actor and preserve selected contract count rather than summing");
  check(Build([ordinary with {Kills=3},second]).Length==2&&Build([ordinary with {Kills=3},second]).All(m=>m.Needed==2),"Completing one of duplicate requirements leaves the other visible");
  settings.WorldMarkers=false;check(Build().Length==2&&Build().All(m=>!m.Elite),"Elite off does not disable ordinary labels");
  settings.WorldMarkers=true;settings.OrdinaryWorldMarkers=false;check(Build().Single().Elite,"Ordinary off leaves elite labels enabled");
  settings.OrdinaryWorldMarkers=true;settings.OrdinaryWorldDistance=50;settings.WorldDistance=300;
  WorldHuntActor[] distant=[new(20,1,new(0,60,0),false),new(21,1,new(50,0,0),false),new(22,2,new(100,0,0),false)];
  check(Build(npcs:distant).Select(m=>m.ActorId).Order().SequenceEqual(new ulong[]{21,22}),"Independent ordinary range uses 3D distance, includes boundary, leaves elite range unchanged");
  check(Build(npcs:[new(0,1,Vector3.Zero,false),new(30,1,new(float.NaN,0,0),false),new(31,1,new(0,float.PositiveInfinity,0),false)]).Length==0,"Invalid actor IDs and positions never become labels");
  check(Build(npcs:[actors[0],actors[0]]).Length==1,"Duplicate object-table entries do not duplicate labels");
  check(WorldHuntMarkers.Build([ordinary],actors,new(float.NaN,0,0),10,20,settings).Length==0,"Invalid player location hides overlay");
  check(WorldHuntMarkers.Build([ordinary],actors,Vector3.Zero,0,0,settings).Length==0,"Unknown zone/map hides overlay");
  settings.OrdinaryWorldDistance=-1;settings.Validate();check(settings.OrdinaryWorldDistance==50&&settings.WorldDistance==300,"Ordinary range validation is independent");
  var legacy=JsonSerializer.Deserialize<HuntSettings>("{\"WorldMarkers\":false,\"WorldDistance\":450}")!;check(legacy.OrdinaryWorldMarkers&&legacy.OrdinaryWorldDistance==150&&!legacy.WorldMarkers&&legacy.WorldDistance==450,"Existing settings gain ordinary overlay defaults without changing elite preference");
  string folder=Path.Combine(Path.GetTempPath(),"HuntRoute-overlay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"settings.json");
  try{var file=new HuntSettingsFile{Settings=new(){WorldMarkers=true,WorldDistance=450,OrdinaryWorldMarkers=false,OrdinaryWorldDistance=80}};file.Save(path);var loaded=HuntSettingsFile.Load(path,new());check(!loaded.Settings.OrdinaryWorldMarkers&&loaded.Settings.OrdinaryWorldDistance==80&&loaded.Settings.WorldDistance==450,"Ordinary toggle and range persist separately from elite settings");}finally{foreach(string name in Directory.GetFiles(folder))File.Delete(name);Directory.Delete(folder);}
  // Repeated rendering decisions never count a death, complete a contract or mutate settings.
  string before=JsonSerializer.Serialize(settings);for(int i=0;i<100;i++)Build(key:"daily",id:11);check(before==JsonSerializer.Serialize(settings)&&ordinary.Kills==1,"Overlay selection is read-only across repeated frames");
 }
}
