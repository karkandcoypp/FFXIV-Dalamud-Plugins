using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

public readonly record struct BillState(bool Unlocked,bool Obtained,byte ObtainedId,byte AvailableId,bool Completed,bool? PeriodCompleted=null) {
    public static BillState FromGame(bool unlocked,bool obtained,byte id,byte available,bool completed,uint count) =>
        new(unlocked,obtained && id>0 && id<=count,id<=count?id:(byte)0,available<=count?available:(byte)0,completed && id>0 && id<=count);
    public bool Taken => Obtained && ObtainedId!=0;
    public bool CompletedCurrentSet => PeriodCompleted ?? (Completed && ObtainedId!=0 && AvailableId==ObtainedId);
    public bool HasNewSet => (Taken || Completed) && AvailableId!=0 && AvailableId!=ObtainedId;
}
public readonly record struct ContractProgress(int Total,int Taken,int Completed,int NewSets) {
    public bool AllCompleted => Total>0 && Completed==Total && NewSets==0;
    public string Label => Total==0 ? "Охота не открыта"
        : AllCompleted ? "Контракты выполнены"
        : NewSets>0 && Taken==0 ? "Доступны новые контракты"
        : NewSets>0 ? $"Взято {Taken}/{Total} · есть новые"
        : Taken==0 && Completed>0 ? $"Выполнено {Completed} из {Total}"
        : Taken==0 ? "Контракты не взяты"
        : Taken<Total ? $"Взято {Taken} из {Total}"
        : Completed==Total ? "Контракты выполнены" : "Контракты взяты";
    public static ContractProgress Summarize(IEnumerable<byte> indices,IReadOnlyDictionary<byte,BillState> snapshots) {
        var bills=indices.Select(i=>snapshots.GetValueOrDefault(i)).Where(s=>s.Unlocked).ToArray();
        return new(bills.Length,bills.Count(s=>s.Taken),bills.Count(s=>s.CompletedCurrentSet),bills.Count(s=>s.HasNewSet));
    }
}

public readonly record struct BillRequirement(byte Mob,uint Kills);
public static class BillCompletion {
    public static bool IsComplete(byte id,uint orderCount,IEnumerable<BillRequirement> requirements,System.Func<byte,int> kills) {
        if(id==0 || id>orderCount) return false;
        bool any=false;
        foreach(var requirement in requirements) {
            if(requirement.Mob>=5 || requirement.Kills==0 || kills(requirement.Mob)<requirement.Kills) return false;
            any=true;
        }
        return any;
    }
}
