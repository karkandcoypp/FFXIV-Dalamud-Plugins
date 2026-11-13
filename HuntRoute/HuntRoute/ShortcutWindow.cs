namespace HuntRoute;

public sealed class ShortcutWindow(Plugin plugin) {
    private ShortcutOffer? latest;
    private ConfirmationRequest? request;
    private bool recalculating;
    private bool Current(ShortcutOffer offer)=>plugin.ShortcutContextMatches(offer)
        &&(recalculating||latest is {} current&&ShortcutAdvisor.SamePresentation(offer,current));
    internal void Sync() {
        var offer=plugin.ReadShortcut(out bool waiting);
        latest=offer;
        recalculating=waiting;
        if(waiting)return;
        if(offer==null) { request=null;plugin.NativeDialogs.Cancel("shortcut");return; }
        string key=$"shortcut:{offer.RunId}:{offer.ContractKey}";
        if(plugin.NativeDialogs.WasDeclined(key))return;
        if(request==null||request.Key!=key||!request.Valid()) {
            var content=TravelDialogContent.Shortcut(offer,plugin.Session.Target?.Name??"",plugin.Session.Target?.TerritoryName);
            request=new("shortcut",key,content.Title,()=>Current(offer),
                ()=>!plugin.TravelBusy&&plugin.NavigationAllowed,()=>plugin.AcceptShortcut(offer),
                content,DeclineKey:key,ConfirmReady:()=>!recalculating);
        }
        plugin.NativeDialogs.Offer(request);
    }
}
