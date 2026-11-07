using System.Collections.Generic;
using System.Linq;
namespace HuntRoute;

public readonly record struct BillState(bool Unlocked,bool Obtained,byte ObtainedId,byte AvailableId,bool Completed) {
    public bool Taken => Obtained && ObtainedId!=0;
    public bool HasNewSet => Taken && AvailableId!=0 && AvailableId!=ObtainedId;
}
public readonly record struct ContractProgress(int Total,int Taken,int Completed,int NewSets) {
    public string Label => Total==0 ? "Охота не открыта"
        : Taken==0 ? "Контракты не взяты"
        : NewSets>0 ? $"Взято {Taken}/{Total} · есть новые"
        : Taken<Total ? $"Взято {Taken} из {Total}"
        : Completed==Total ? "Контракты выполнены" : "Контракты взяты";
    public static ContractProgress Summarize(IEnumerable<byte> indices,IReadOnlyDictionary<byte,BillState> snapshots) {
        var bills=indices.Select(i=>snapshots.GetValueOrDefault(i)).Where(s=>s.Unlocked).ToArray();
        return new(bills.Length,bills.Count(s=>s.Taken),bills.Count(s=>s.Taken&&s.Completed),bills.Count(s=>s.HasNewSet));
    }
}
