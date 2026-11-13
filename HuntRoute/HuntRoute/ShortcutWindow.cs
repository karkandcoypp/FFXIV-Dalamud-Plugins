namespace HuntRoute;

public sealed class ShortcutWindow(Plugin plugin) {
    private readonly ShortcutNotice notice=new();
    internal void Sync() {
        var offer=plugin.ReadShortcut(out bool waiting);
        if(waiting)return;
        if(offer==null) { plugin.NativeDialogs.Cancel("shortcut");return; }
        if(!notice.ShouldOpen(offer)) return;
        plugin.NativeDialogs.Offer(new("shortcut",$"shortcut:{offer.RunId}:{offer.ContractKey}",
            $"До цели {plugin.Session.Target?.Name}: {offer.DirectDistance:F0} м.\n\nЭфирит: {offer.Name}\nПосле телепорта до цели: {offer.FinalDistance:F0} м\nСтоимость: {offer.GilCost} гил\n\nОценка времени: перелёт ~{offer.DirectSeconds:F0} с\nС телепортом ~{offer.TeleportSeconds:F0} с\nЭкономия ~{offer.SavedSeconds:F0} с\n\nСократить перелёт телепортом?",
            ()=>plugin.ShortcutStillValid(offer),
            ()=>!plugin.TravelBusy && plugin.NavigationAllowed,
            ()=>plugin.AcceptShortcut(offer),
            TravelDialogContent.Shortcut(offer,plugin.Session.Target?.Name ?? "Цель контракта",plugin.Session.Target?.TerritoryName)));
    }
}
