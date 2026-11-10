namespace HuntRoute;

public sealed class NextZoneWindow(Plugin plugin) {
    private readonly NextZoneNotice notice=new();
    internal void Sync() {
        var session=plugin.Session;
        if(!plugin.NavigationAllowed || session.Phase!=RoutePhase.NextZone || session.Suggested is not {} next || session.Target is not {} from) {
            plugin.NativeDialogs.Cancel("next");return;
        }
        if(Service.ClientState.TerritoryType==next.TerritoryType){plugin.NativeDialogs.Cancel("next");return;}
        uint run=session.RunId,zone=next.TerritoryType;string targetKey=JourneyFlow.Key(next);
        string declineKey=$"next:{run}:{from.TerritoryType}:{targetKey}";
        if(plugin.NativeDialogs.WasDeclined(declineKey))return;
        var entry=plugin.Flow.Active?plugin.EntryFor(next):null;uint quote=entry==null?0:plugin.Price(entry.Id);
        if(!notice.ShouldOpen(session,entry?.Id??0,quote))return;
        plugin.NextOffered();
        plugin.NativeDialogs.Offer(new("next",$"{declineKey}:{entry?.Id??0}:{quote}",
            $"Цели маршрута в зоне {from.TerritoryName} выполнены.\n\nСледующая зона: {next.TerritoryName}\nПервая цель: {next.Name}\n\nТелепортироваться и продолжить охоту?",
            ()=>plugin.Ready && Service.ClientState.TerritoryType!=zone && (!plugin.Flow.Active||plugin.Eligible(next)) && session.RunId==run && session.Phase==RoutePhase.NextZone && session.Suggested?.TerritoryType==zone && JourneyFlow.Key(session.Suggested)==targetKey && (entry==null || plugin.EntryFor(next)?.Id==entry.Id && plugin.Price(entry.Id)==quote),
            ()=>!plugin.TravelBusy && plugin.NavigationAllowed,
            ()=> { var target=session.Suggested;if(target==null || entry!=null && (plugin.Price(entry.Id)>quote || plugin.EntryFor(target)?.Id!=entry.Id)) return false;plugin.Travel(target);return session.Phase!=RoutePhase.NextZone; },
            TravelDialogContent.Next(from.TerritoryName ?? "Текущая зона",next.TerritoryName ?? "Следующая зона",next.Name ?? "Цель контракта") with {Detail=plugin.TravelHint(next,entry),Context=entry==null?"Цели текущей зоны выполнены.":$"{entry.Name} · {quote} гил"},DeclineKey:declineKey));
    }
}
