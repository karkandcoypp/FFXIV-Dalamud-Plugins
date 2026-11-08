namespace HuntRoute;

public sealed class ShortcutWindow(Plugin plugin) {
    private readonly ShortcutNotice notice=new();
    internal void Sync() {
        var offer=plugin.FindShortcut();
        if(offer==null) { plugin.NativeDialogs.Cancel("shortcut");return; }
        if(!notice.ShouldOpen(offer)) return;
        plugin.NativeDialogs.Offer(new("shortcut",$"shortcut:{offer.RunId}:{offer.Bill}:{offer.Mob}:{offer.TargetId}",
            $"До цели {plugin.Session.Target?.Name}: {offer.DirectDistance:F0} м.\n\nЭфирит: {offer.Name}\nПосле телепорта до цели: {offer.FinalDistance:F0} м\nСтоимость: {offer.GilCost} гил\n\nОценка времени: перелёт ~{offer.DirectSeconds:F0} с\nС телепортом ~{offer.TeleportSeconds:F0} с\nЭкономия ~{offer.SavedSeconds:F0} с\n\nСократить перелёт телепортом?",
            ()=>plugin.Ready && plugin.Session.RunId==offer.RunId && plugin.Session.Phase==RoutePhase.Following
                && plugin.Session.Target is {} target && target.BillNumber==offer.Bill && target.MobIndex==offer.Mob && target.MobHuntId==offer.TargetId,
            ()=>!plugin.TravelBusy,
            ()=>plugin.AcceptShortcut(offer),
            TravelDialogContent.Shortcut(offer,plugin.Session.Target?.TerritoryName ?? "Текущая зона",plugin.Session.Target?.Name ?? "Цель контракта")));
    }
}
