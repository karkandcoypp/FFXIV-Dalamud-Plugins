using System.Collections.Concurrent;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendorProfit;

public sealed unsafe class NativeBuyGateway : IBuyGateway, IDisposable
{
    private readonly IMarketBoard board;
    private readonly IGameGui gui;
    private readonly IFramework framework;
    private readonly ConcurrentDictionary<ulong, Lot> fresh = new();
    private volatile uint requested;
    public NativeBuyGateway(IMarketBoard service, IGameGui gameGui, IFramework gameFramework) { board = service; gui = gameGui; framework = gameFramework; board.OfferingsReceived += OnOfferings; }
    public void Clear() { requested = 0; fresh.Clear(); }
    private void OnOfferings(IMarketBoardCurrentOfferings offerings)
    {
        uint item = requested;
        if (item == 0) return;
        foreach (var l in offerings.ItemListings)
            if (l.ItemId == item && requested == item)
                fresh[l.ListingId] = new(l.ListingId, l.ItemId, l.RetainerName, l.PricePerUnit, l.ItemQuantity, l.TotalTax, l.IsHq, l.OnMannequin);
    }
    public bool Request(uint item)
    {
        if (!framework.IsInFrameworkUpdateThread) return false;
        Clear();
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null || item == 0) return false;
        // Avoid showing a previous item's title over newly requested native listings.
        var result = (AtkUnitBase*)gui.GetAddonByName("ItemSearchResult").Address;
        if (result != null && result->IsVisible) result->Close(true);
        requested = item; proxy->SearchItemId = item;
        if (proxy->RequestData()) return true;
        Clear(); return false;
    }
    private static bool Same(Lot lot, MarketBoardListing native) =>
        lot.Id == native.ListingId && lot.Item == native.ItemId && lot.Price == native.UnitPrice &&
        lot.Quantity == native.Quantity && lot.Tax == native.TotalTax && lot.Hq == native.IsHqItem &&
        native.RetainerId != 0 && !native.IsMannequin && !native.IsSellingAsSet;
    public Lot[] FreshLots(uint item)
    {
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null || requested != item || proxy->SearchItemId != item) return [];
        var output = new List<Lot>();
        foreach (var l in proxy->Listings)
            if (fresh.TryGetValue(l.ListingId, out var lot) && Same(lot, l)) output.Add(lot);
        return output.ToArray();
    }
    public bool Submit(Lot lot)
    {
        if (!framework.IsInFrameworkUpdateThread) return false;
        var proxy = InfoProxyItemSearch.Instance();
        if (proxy == null || requested != lot.Item || proxy->SearchItemId != lot.Item ||
            !fresh.TryGetValue(lot.Id, out var received) || received != lot) return false;
        foreach (var entry in proxy->Listings)
        {
            if (!Same(lot, entry)) continue;
            var native = entry;
            if (!proxy->SetLastPurchasedItem(&native)) return false;
            // Only the game's existing native purchase path, with exact fresh listing data.
            return proxy->SendPurchaseRequestPacket();
        }
        return false;
    }
    public int ItemCount(uint item, bool hq)
    {
        var manager = InventoryManager.Instance();
        return manager == null ? -1 : manager->GetInventoryItemCount(item, hq, false, false);
    }
    public void Dispose() { Clear(); board.OfferingsReceived -= OnOfferings; }
}
