namespace HuntRoute;

public sealed class NextZoneWindow(Plugin plugin) {
    private readonly NextZoneNotice notice=new();
    internal void Sync() {
        var session=plugin.Session;
        if(session.Phase!=RoutePhase.NextZone || session.Suggested is not {} next || session.Target is not {} from) {
            plugin.NativeDialogs.Cancel("next");return;
        }
        if(!notice.ShouldOpen(session)) return;
        uint run=session.RunId,zone=next.TerritoryType;
        plugin.NativeDialogs.Offer(new("next",$"next:{run}:{from.TerritoryType}:{zone}",
            $"Цели маршрута в зоне {from.TerritoryName} выполнены.\n\nСледующая зона: {next.TerritoryName}\nПервая цель: {next.Name}\n\nТелепортироваться и продолжить охоту?",
            ()=>plugin.Ready && session.RunId==run && session.Phase==RoutePhase.NextZone && session.Suggested?.TerritoryType==zone,
            ()=>!plugin.TravelBusy,
            ()=> { var target=session.Suggested;if(target==null) return false;plugin.Travel(target);return session.Phase!=RoutePhase.NextZone; },
            TravelDialogContent.Next(from.TerritoryName ?? "Текущая зона",next.TerritoryName ?? "Следующая зона",next.Name ?? "Цель контракта")));
    }
}
