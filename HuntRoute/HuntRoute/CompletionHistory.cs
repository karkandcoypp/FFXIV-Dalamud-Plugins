using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace HuntRoute;

public sealed class CompletionRecord {
    public long SeenPeriod { get; set; }
    public byte LastOrder { get; set; }
    public bool WasTaken { get; set; }
    public long ActivePeriod { get; set; }
    public byte ActiveOrder { get; set; }
    public long CompletedPeriod { get; set; }
    public byte CompletedOrder { get; set; }
}
public sealed class CompletionHistory {
    private bool skipBackup;
    public bool RequiresPickupEvidence {get;set;}
    [JsonIgnore] public bool Recovered {get;private set;}
    public int Version { get; set; }=1;
    public Dictionary<ulong,Dictionary<byte,CompletionRecord>> Characters { get; set; }=[];
    [JsonIgnore] public bool Dirty { get; private set; }
    private static long Period(DateTimeOffset now,bool weekly)=>ResetClock.Period(now,weekly).ToUnixTimeSeconds();
    public void Observe(ulong character,byte bill,bool weekly,DateTimeOffset now,BillState state) {
        if(character==0 || !state.Unlocked) return;
        if(!Characters.TryGetValue(character,out var bills)) { Characters[character]=bills=[];Dirty=true; }
        if(!bills.TryGetValue(bill,out var record)) { bills[bill]=record=new();Dirty=true; }
        long period=Period(now,weekly);
        // A newly held, unfinished current-set bill proves a fresh pickup, even if IDs wrap.
        if(state.Taken && !state.Completed && state.AvailableId==state.ObtainedId && state.ObtainedId!=0
            && (record.LastOrder==0 || record.LastOrder!=state.ObtainedId || !record.WasTaken || record.ActivePeriod==period)) {
            if(record.ActivePeriod!=period || record.ActiveOrder!=state.ObtainedId) { record.ActivePeriod=period;record.ActiveOrder=state.ObtainedId;Dirty=true; }
        }
        bool fresh=record.LastOrder==0 || record.LastOrder!=state.ObtainedId
            || (record.ActivePeriod==period && record.ActiveOrder==state.ObtainedId);
        if(state.CompletedCurrentSet && fresh && (!RequiresPickupEvidence || record.ActivePeriod==period && record.ActiveOrder==state.ObtainedId) && (record.CompletedPeriod!=period || record.CompletedOrder!=state.ObtainedId)) {
            record.CompletedPeriod=period;record.CompletedOrder=state.ObtainedId;Dirty=true;
        }
        if(record.SeenPeriod!=period) { record.SeenPeriod=period;Dirty=true; }
        if(state.ObtainedId!=0 && record.WasTaken!=state.Taken) { record.WasTaken=state.Taken;Dirty=true; }
        if(state.ObtainedId!=0 && record.LastOrder!=state.ObtainedId) { record.LastOrder=state.ObtainedId;Dirty=true; }
    }
    public bool IsComplete(ulong character,byte bill,bool weekly,DateTimeOffset now,byte available=0) =>
        character!=0 && Characters.TryGetValue(character,out var bills) && bills.TryGetValue(bill,out var record)
        && record.CompletedPeriod==Period(now,weekly) && record.CompletedOrder!=0
        && (available==0 || available==record.CompletedOrder);
    public bool IsPreviousCompletion(ulong character,byte bill,bool weekly,DateTimeOffset now,byte order) =>
        order!=0 && Characters.TryGetValue(character,out var bills) && bills.TryGetValue(bill,out var record)
        && record.CompletedOrder==order && record.CompletedPeriod!=0 && record.CompletedPeriod<Period(now,weekly);
    public static CompletionHistory Load(string path) {
        string json;
        try{json=File.ReadAllText(path);}
        catch(FileNotFoundException){return new();}
        catch(DirectoryNotFoundException){return new();}
        var history=JsonSerializer.Deserialize<CompletionHistory>(json) ?? throw new InvalidDataException("Empty completion history");
        if(history.Version!=1 || history.Characters==null) throw new InvalidDataException("Unsupported completion history");
        foreach(var bills in history.Characters.Values) {
            if(bills==null) throw new InvalidDataException("Invalid character history");
            foreach(var pair in bills) if(pair.Key>=22 || pair.Value==null) throw new InvalidDataException("Invalid bill history");
        }
        return history;
    }
    public static CompletionHistory LoadRecovering(string path) {
        try{return Load(path);}catch(Exception e)when(e is JsonException or InvalidDataException or NotSupportedException){
            File.Copy(path,path+".invalid-"+DateTime.UtcNow.Ticks,false);
            CompletionHistory? history=null;
            if(File.Exists(path+".bak"))try{history=Load(path+".bak");}catch(Exception backup)when(backup is JsonException or InvalidDataException or NotSupportedException){}
            history??=new(){RequiresPickupEvidence=true};history.skipBackup=true;history.Recovered=true;history.Dirty=true;return history;
        }
    }
    public void Save(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary=path+".tmp";
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)) {
            JsonSerializer.Serialize(stream,this);stream.Flush(true);
        }
        if(!skipBackup&&File.Exists(path))File.Copy(path,path+".bak",true);
        File.Move(temporary,path,true);skipBackup=false;Dirty=false;
    }
}
