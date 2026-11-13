namespace HuntRoute;

public sealed class ShortcutWindow(Plugin plugin) {
    private ShortcutOffer? latest;
    private ConfirmationRequest? request;
    private bool recalculating;
    private bool Current(ShortcutOffer offer)=>plugin.ShortcutContextMatches(offer)
        &&(recalculating||latest is {} current&&current.UsesNavmesh==offer.UsesNavmesh&&ShortcutAdvisor.MatchesQuote(offer,current));
    internal void Sync() {
        var offer=plugin.ReadShortcut(out bool waiting);
        latest=offer;
        recalculating=waiting;
        if(waiting)return;
        if(offer==null) { request=null;plugin.NativeDialogs.Cancel("shortcut");return; }
        string key=$"shortcut:{offer.RunId}:{offer.ContractKey}";
        if(plugin.NativeDialogs.WasDeclined(key))return;
        if(request==null||request.Key!=key||!request.Valid())request=new("shortcut",key,
            $"Р”Рѕ С†РµР»Рё {plugin.Session.Target?.Name}: {offer.DirectDistance:F0} Рј.\n\nР­С„РёСЂРёС‚: {offer.Name}\nРџРѕСЃР»Рµ С‚РµР»РµРїРѕСЂС‚Р° РґРѕ С†РµР»Рё: {offer.FinalDistance:F0} Рј\nРЎС‚РѕРёРјРѕСЃС‚СЊ: {offer.GilCost} РіРёР»\n\nРћС†РµРЅРєР° РІСЂРµРјРµРЅРё: РїРµСЂРµР»С‘С‚ ~{offer.DirectSeconds:F0} СЃ\nРЎ С‚РµР»РµРїРѕСЂС‚РѕРј ~{offer.TeleportSeconds:F0} СЃ\nР­РєРѕРЅРѕРјРёСЏ ~{offer.SavedSeconds:F0} СЃ\n\nРЎРѕРєСЂР°С‚РёС‚СЊ РїРµСЂРµР»С‘С‚ С‚РµР»РµРїРѕСЂС‚РѕРј?",
            ()=>Current(offer),
            ()=>!plugin.TravelBusy && plugin.NavigationAllowed,
            ()=>plugin.AcceptShortcut(offer),
            TravelDialogContent.Shortcut(offer,plugin.Session.Target?.Name ?? "Р¦РµР»СЊ РєРѕРЅС‚СЂР°РєС‚Р°",plugin.Session.Target?.TerritoryName), DeclineKey:key, ConfirmReady:()=>!recalculating);
        plugin.NativeDialogs.Offer(request);
    }
}
