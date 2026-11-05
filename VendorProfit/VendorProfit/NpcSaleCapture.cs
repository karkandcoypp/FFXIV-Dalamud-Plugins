using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace VendorProfit;

public sealed unsafe partial class Plugin
{
    private readonly NpcSaleObserver npcSales = new();
    private string npcSaleStatus = "Продажи обычным NPC учитываются автоматически, пока плагин включён.";
    private bool npcShopDetected;
    private NpcShopSnapshot? ReadNpcShop()
    {
        var agent=AgentShop.Instance();
        if(agent==null || !agent->IsAgentActive())return null;
        var proxy=ShopEventHandler.AgentProxy.Instance();
        if(proxy==null || proxy->Handler==null || (nint)agent->EventReceiver!=(nint)proxy)
        {npcSaleStatus="Этот магазин не распознан для автоучёта; доступна ручная корректировка.";return null;}
        var shop=proxy->Handler;
        if(shop->IsTradingWithRetainer || shop->CurrentMode is <1 or >2 || shop->BuybackCount is <0 or >10)return null;
        var manager=InventoryManager.Instance();if(manager==null)return null;
        var stock=new Dictionary<StockKey,long>();
        foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4})
        {
            var bag=manager->GetInventoryContainer(type);
            if(bag==null || !bag->IsLoaded || bag->Items==null || bag->Size is <0 or >100)return null;
            for(int i=0;i<bag->Size;i++)
            {
                var item=bag->Items+i;if(item->ItemId==0 || item->Quantity==0)continue;
                var key=new StockKey(item->ItemId,(item->Flags&InventoryItem.ItemFlags.HighQuality)!=0);
                stock[key]=stock.GetValueOrDefault(key)+item->Quantity;
            }
        }
        var sheet=Data.GetExcelSheet<Item>();var back=new List<BuybackStock>();
        for(int i=0;i<shop->BuybackCount;i++)
        {
            var b=shop->Buyback[i];
            bool encodedHq=b.ItemId>=1_000_000 && b.ItemId<2_000_000;uint id=encodedHq?b.ItemId-1_000_000:b.ItemId;
            if(b.Quantity<=0 || id==0 || sheet.GetRowOrDefault(id) is not {} item)continue;
            bool hq=encodedHq || (b.Flags&InventoryItem.ItemFlags.HighQuality)!=0;long unit=Policy.NpcPrice(item.PriceLow,hq);
            if(unit<=0 || b.Price!=unit && b.Price!=unit*b.Quantity)continue;
            back.Add(new(new(id,hq),(uint)b.Quantity,unit));
        }
        NpcSaleIntent? intent=null;
        if(shop->TransactionType==2 && shop->WaitingForTransactionToFinish && !shop->WaitingForSellConfirm &&
            shop->SellInventoryType is InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4 &&
            shop->SellInventorySlot>=0 && shop->SellInventorySlot<35 && shop->TransactionItemCount>0)
        {
            var slot=manager->GetInventorySlot(shop->SellInventoryType,shop->SellInventorySlot);
            if(slot!=null && slot->ItemId==shop->TransactionItemId && sheet.GetRowOrDefault(slot->ItemId) is {} item)
            {
                bool hq=(slot->Flags&InventoryItem.ItemFlags.HighQuality)!=0;long unit=Policy.NpcPrice(item.PriceLow,hq);
                if(unit>0 && (shop->SellPrice==unit || shop->SellPrice==unit*shop->TransactionItemCount))
                    intent=new(new(slot->ItemId,hq),(uint)shop->TransactionItemCount,unit,shop->SellInventorySlot);
            }
        }
        return new(DateTimeOffset.UtcNow,statistics.Character,statisticsSession?.World??0,(nint)shop,manager->GetGil(),stock,back.ToArray(),intent);
    }
    private void UpdateNpcSalesSafely()
    {
        if(!statisticsValid || statisticsSession==null || statistics.Purchases.Count==0){npcSales.Reset();npcShopDetected=false;return;}
        try
        {
            var frame=ReadNpcShop();
            if(frame!=null && !npcShopDetected)npcSaleStatus="Магазин NPC распознан. Ожидаю подтверждённую продажу.";
            npcShopDetected=frame!=null;
            var changes=npcSales.Observe(frame);uint total=0;
            foreach(var change in changes)total+=NpcSaleObserver.Apply(statistics,change);
            if(total>0)
            {
                statisticsDirty=true;SaveStatistics();
                npcSaleStatus=changes[0].Buyback?$"Обратный выкуп: отменён учёт продажи {total} шт.":$"Автоматически учтена продажа NPC: {total} шт.";
            }
            else if(npcSales.Skipped)npcSaleStatus="Последнюю операцию не удалось однозначно подтвердить. Доступна ручная корректировка.";
        }
        catch(Exception e){npcSales.Reset();npcSaleStatus="Не удалось обновить продажи NPC: "+e.Message;Log.Warning(e,"VendorProfit NPC sale statistics failed");}
    }
}
