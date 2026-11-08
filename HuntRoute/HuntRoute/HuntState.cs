using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace HuntRoute;
[Flags] public enum PauseReason { None=0,User=1,Restored=2,Marker=4,NoZone=8,TravelError=16,Unavailable=32,Board=64,FlagError=128 }
public sealed class HuntSettings {
 public bool WorldMarkers {get;set;}=true;
 public int WorldDistance {get;set;}=300;
 public bool OrdinaryWorldMarkers {get;set;}=true;
 public int OrdinaryWorldDistance {get;set;}=150;
 public bool Mini {get;set;}
 public bool EliteSound {get;set;}
 public bool NextZoneSound {get;set;}
 // Read old combined preference, but write only the independent preferences.
 [JsonPropertyName("Sound"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]
 public bool? LegacySound {get=>null;set{if(value is {} enabled){EliteSound=enabled;NextZoneSound=enabled;}}}
 public bool ResetNotifications {get;set;}=true;
 public bool Shortcuts {get;set;}=true;
 public uint MaxGil {get;set;}=200;
 public float MinSeconds {get;set;}=10;
 public float MinPercent {get;set;}=20;
 public void Validate(){WorldDistance=Math.Clamp(WorldDistance,50,500);OrdinaryWorldDistance=Math.Clamp(OrdinaryWorldDistance,50,500);if(!float.IsFinite(MinSeconds)||MinSeconds<0)MinSeconds=10;if(!float.IsFinite(MinPercent)||MinPercent<0||MinPercent>100)MinPercent=20;}
}
public sealed class ContractEpisode {public byte Order {get;set;} public bool Taken {get;set;} public uint Generation {get;set;} }
public sealed class SavedJourney {
 public Dictionary<byte,ContractEpisode> Contracts {get;set;}=[];
 public bool Active {get;set;}
 public string? Target {get;set;}
 public string? Manual {get;set;}
 public HashSet<string> Deferred {get;set;}=[];
 public HashSet<uint> Zones {get;set;}=[];
 public HashSet<string> LongNotified {get;set;}=[];
 public long DailyNotice {get;set;}
 public long WeeklyNotice {get;set;}
 public string? Context {get;set;}
 public string? SearchTarget {get;set;}
 public EliteProgress? Search {get;set;}
}
public sealed class HuntStore {
 public int Version {get;set;}=1;
 [JsonIgnore] public HuntSettings Settings {get;set;}=new();
 public Dictionary<ulong,SavedJourney> Characters {get;set;}=[];
 private bool skipBackup;
 private static readonly JsonSerializerOptions Options=new(){IncludeFields=true};
 public static HuntStore Load(string path){
  if(!File.Exists(path))return new();
  try{return Parse(File.ReadAllText(path));}catch(Exception e)when(e is JsonException or InvalidDataException or NotSupportedException){
   File.Copy(path,path+".invalid-"+DateTime.UtcNow.Ticks,false);
   if(File.Exists(path+".bak")){try{var recovered=Parse(File.ReadAllText(path+".bak"));recovered.skipBackup=true;return recovered;}catch(Exception fallback)when(fallback is JsonException or InvalidDataException or NotSupportedException){}}
   return new(){skipBackup=true};
  }
 }
 private static HuntStore Parse(string json){var value=JsonSerializer.Deserialize<HuntStore>(json,Options)??throw new InvalidDataException("Empty route state");if(value.Version!=1||value.Characters==null||value.Characters.Values.Any(v=>v==null||v.Deferred==null||v.Zones==null||v.LongNotified==null||v.Contracts==null||v.Contracts.Any(p=>p.Key>=22||p.Value==null)))throw new InvalidDataException("Invalid route state");using var doc=JsonDocument.Parse(json);if(doc.RootElement.TryGetProperty("Settings",out var legacy))value.Settings=legacy.Deserialize<HuntSettings>()??new();value.Settings.Validate();return value;}
 public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string temp=path+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,this,Options);stream.Flush(true);}if(!skipBackup&&File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temp,path,true);skipBackup=false;}
}
public sealed class JourneyFlow {
 public bool Active;
 public PauseReason Pauses;
 public string? Manual;
 public readonly HashSet<string> Deferred=[];
 public readonly HashSet<uint> Zones=[];
 public readonly HashSet<string> LongNotified=[];
 public readonly Dictionary<string,string> Unavailable=[];
 public string Status="";
 public bool Restored;
 public string? RestoreTarget;
 public bool CanNavigate=>Active && Pauses==PauseReason.None;
 public static string Key(MobHuntEntry m)=>$"{m.BillNumber}:{m.OrderId}:{m.Generation}:{m.MobIndex}:{m.MobHuntId}";
 public bool Excluded(MobHuntEntry m)=>Deferred.Contains(Key(m))||Zones.Contains(m.TerritoryType);
 public void Load(SavedJourney s){Active=s.Active;Pauses=Active?PauseReason.Restored:PauseReason.None;Manual=s.Manual;Deferred.UnionWith(s.Deferred);Zones.UnionWith(s.Zones);LongNotified.UnionWith(s.LongNotified);RestoreTarget=s.Target;Restored=Active;}
 public void Stop(){Active=false;Pauses=PauseReason.None;Manual=null;Deferred.Clear();Zones.Clear();LongNotified.Clear();RestoreTarget=null;Restored=false;Status="Маршрут остановлен";}
}
public sealed record PendingJourney(uint Source,uint Destination,uint Map,long Started,string? Target,bool Board) {
 public System.Numerics.Vector2? Origin {get;set;}
 public System.Numerics.Vector2? ArrivalPoint {get;set;}
 public bool Loaded {get;set;}
 public bool CastingSeen {get;set;}
 public long? CastStopped {get;set;}
 public bool Expired(long now)=>now>=Started && now-Started>=120000;
}
