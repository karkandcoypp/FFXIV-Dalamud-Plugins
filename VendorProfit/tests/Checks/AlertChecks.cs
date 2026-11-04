using System.Text.Json;
using VendorProfit;

static class AlertChecks
{
    public static void Run(Action<bool,string> check)
    {
        var now=DateTimeOffset.UtcNow;
        var item=new VendorItem(8144,"Clear Demimateria III",5000,false,99,false);
        var lot=new Lot(700,8144,"retainer",100,1,5,false);
        var quote=Policy.Evaluate(item,lot)!;
        var suggestion=new Suggestion(item,quote,now,false,80,"Cerberus");
        var snapshot=new ScanResult(80,now,[suggestion],1,0,0,"external",Scope:"External");
        var empty=new HashSet<string>();
        Suggestion[] Select(ScanResult input, IReadOnlySet<string>? seen=null, bool chaos=false) =>
            AlertPolicy.NewLots(input,80,chaos,now,180,1000,500000,1,seen ?? empty);
        check(Select(snapshot).Length==1,"alerts: fresh profitable offer selected without any game gateway");
        var seen=new HashSet<string>{AlertPolicy.Key(suggestion)};
        check(Select(snapshot,seen).Length==0,"alerts: already announced listing suppressed");
        check(Select(snapshot with { Suggestions=[suggestion with { Quote=quote with { Cost=104, Lot=lot with { Price=99 } } }] },seen).Length==0,
            "alerts: price changes alone do not spam for a known listing ID");
        check(Select(snapshot with { Suggestions=[suggestion with { Quote=quote with { Lot=lot with { Id=701 } } }] },seen).Length==1,
            "alerts: new listing identity produces a new notification");
        check(Select(snapshot with { Finished=now.AddMinutes(-3) }).Length==0 &&
            Select(snapshot with { Suggestions=[suggestion with { Updated=now.AddHours(-4) }] }).Length==0,
            "alerts: stale snapshots and stale books suppressed");
        var foreign=suggestion with { World=83, WorldName="Louisoix" };
        check(Select(snapshot with { Suggestions=[foreign] }).Length==0 && Select(snapshot with { Suggestions=[foreign] },chaos:true).Length==1,
            "alerts: current-world filter and Chaos scope remain explicit");
        check(Select(snapshot with { Suggestions=[suggestion with { Quote=quote with { Cost=1 } }] }).Length==0,
            "alerts: inconsistent tax-inclusive money values suppressed");
        var stored=JsonSerializer.Deserialize<HashSet<string>>(JsonSerializer.Serialize(seen))!;
        check(Select(snapshot,stored).Length==0,"alerts: persisted history prevents restart duplicates");
        check(AlertPolicy.NewLots(snapshot,80,false,now,180,1000,100,1,empty).Length==0,
            "alerts: notification cost ceiling includes buyer tax");
    }
}
