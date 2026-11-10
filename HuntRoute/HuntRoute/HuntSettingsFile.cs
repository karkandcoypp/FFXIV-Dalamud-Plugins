using System;
using System.IO;
using System.Text.Json;
namespace HuntRoute;
public sealed class HuntSettingsFile {
 public int Version {get;set;}=1;
 public HuntSettings Settings {get;set;}=new();
 private bool skipBackup;
 public bool Recovered {get;private set;}
 public static HuntSettingsFile Load(string path,HuntSettings legacy){
  string json;
  try{json=File.ReadAllText(path);}
  catch(FileNotFoundException){return new(){Settings=legacy};}
  catch(DirectoryNotFoundException){return new(){Settings=legacy};}
  try{return Parse(json);}catch(Exception e)when(e is JsonException or InvalidDataException or NotSupportedException){
   File.Copy(path,path+".invalid-"+DateTime.UtcNow.Ticks,false);
   if(File.Exists(path+".bak"))try{var backup=Parse(File.ReadAllText(path+".bak"));backup.skipBackup=true;backup.Recovered=true;return backup;}catch(Exception fallback)when(fallback is JsonException or InvalidDataException or NotSupportedException){}
   return new(){skipBackup=true,Recovered=true};
  }
 }
 private static HuntSettingsFile Parse(string json){var value=JsonSerializer.Deserialize<HuntSettingsFile>(json)??throw new InvalidDataException("Empty settings");if(value.Version!=1||value.Settings==null)throw new InvalidDataException("Invalid settings");value.Settings.Validate();return value;}
 public void Save(string path){Settings.Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);string temp=path+".tmp";using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,this);stream.Flush(true);}if(!skipBackup&&File.Exists(path))File.Copy(path,path+".bak",true);File.Move(temp,path,true);skipBackup=false;}
}
