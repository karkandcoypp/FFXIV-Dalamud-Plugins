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
  var gradient=selected.OrderBy(m=>m.Distance).ToArray();
  check(gradient.Select(m=>m.DistanceFraction).SequenceEqual(new[]{0f,.5f,1f})&&gradient.Select(m=>m.Color).Distinct().Count()==3,"Marker color follows real distance across ordinary, tracked and elite targets");
  check(gradient[0].Color==0xff5ec3ff&&gradient[2].Color==0xffffd06d,"Nearest marker is gold and furthest is sky blue");
  check(Build(npcs:[actors[0],actors[1] with {Position=actors[0].Position}]).All(m=>m.Color==gradient[0].Color),"Equal distances share the same nearest color");
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
