using System;
using System.Linq;
using ScavengerTeams;

internal static partial class RegressionTests
{
    private static void RunBottleTests()
    {
        Check("bottle route uses original wine containers and registers no bottle crate", () =>
        {
            var team=TeamDefinitions.Bottle;
            Require(team.MaterialId=="wine_bottle" && team.LooseOnly, "wrong bottle definition");
            Require(!DirectoryMaster.Factories.ContainsKey(team.CrateId) && TeamDefinitions.ForCrate(team.CrateId)==null, "fake bottle crate registered");
            BuyUpgrade(1,team); var client=Visit(team,11);
            foreach (var bottle in Stock.Where(i=>i.identifier=="wine_bottle"))
            {
                Require(bottle.IsTag("WINE_CONTAINER_TAG") && bottle.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt()==750, "wine functionality lost");
                Require(bottle.unitValue==12 && bottle.shape.width==1 && bottle.shape.height==4, "original price or shape changed");
            }
            Require(client.wholesaleDiscount==-50, "bottle special missing");
        });
        Check("bottle quantity upgrades do not reset schedule, current shipment or initial discount", () =>
        {
            var team=TeamDefinitions.Bottle;
            BuyUpgrade(1,team); var first=Visit(team,11);
            BuyUpgrade(2,team); first.OnArrival();
            Require(Stock.Count(i=>i.identifier=="wine_bottle")==6 && first.wholesaleDiscount==-50, "live shipment mutated");
            first.OnDismiss(); Reload();
            Require(TeamStateRepository.Read(team).nextDay==14, "upgrade rescheduled");
            var second=Visit(team,14);
            Require(Stock.Count(i=>i.identifier=="wine_bottle")==12 && second.wholesaleDiscount==-15, "wrong second shipment");
            second.OnDismiss(); BuyUpgrade(3,team); Reload();
            Require(TeamStateRepository.Read(team).nextDay==17, "third tier rescheduled");
            var third=Visit(team,17);
            Require(Stock.Count(i=>i.identifier=="wine_bottle")==18 && third.wholesaleDiscount==-15, "wrong third shipment");
        });
        Check("level3 bottle partial sale/reload preserves high sold-mask bits without refilling", () =>
        {
            var team=TeamDefinitions.Bottle; BuyThrough(team,3); var client=Visit(team,11);
            Purchase(client,Stock[17]);
            var state=TeamStateRepository.Read(team);
            Require(DeliveryRules.IsSold(state,17), "high index lost");
            Reload(); client=Visit(team,11);
            Require(Stock.Count==17 && Stock.All(i=>i.identifier=="wine_bottle"), "bought stock regenerated or junk added");
            foreach(var item in Stock.ToArray()) Purchase(client,item);
            Require(TeamStateRepository.Read(team).closed, "18-item shipment did not close");
        });
        Check("bottle route carries no junk and follows native overflow without restoring discarded bottles", () =>
        {
            var team=TeamDefinitions.Bottle; BuyThrough(team,3);
            EmporiumEntry.Instance.frontInvinvElement.Capacity=68;
            Arrive(11);
            Require(Stock.Count==17 && Stock.All(i=>i.identifier=="wine_bottle"), "wrong bottle count or junk added");
            var state=TeamStateRepository.Read(team);
            Require(DeliveryRules.IsSold(state,17), "overflow index not persisted");
            Reload(); var client=Visit(team,11);
            Require(Stock.Count==17 && Stock.All(i=>i.identifier=="wine_bottle") && client.wholesaleDiscount==-50, "discarded bottle resurrected");
        });
        Check("bottle state capacity remains route-specific and rejects indexes outside its shipment", () =>
        {
            var state=new DeliveryState(); DeliveryRules.Arrive(state,11,3);
            DeliveryRules.Sold(state,17,true);
            Require(DeliveryRules.Valid(state,true) && !DeliveryRules.Valid(state), "legacy route silently accepted bottle mask");
            try { DeliveryRules.Sold(state,18,true); throw new Exception("out-of-range index accepted"); }
            catch(ArgumentOutOfRangeException) { }
        });
        Check("bottle service uses own scattered-bottle icon without changing native sprites", () =>
        {
            var sprites=SpriteDict.Instance.spriteDictionary;
            sprites["native-sentinel"]=new object(); var native=sprites["native-sentinel"];
            NetworkRegistration.Register();
            Require(ModHelper.Upgrades[TeamDefinitions.Bottle.UpgradeId(1)].IconKey==ModEntry.Id+":bottle_icon", "wrong service icon");
            NetworkRegistration.RemoveRuntimeIcons();
            Require(!sprites.ContainsKey(ModEntry.Id+".bottle_icon") && ReferenceEquals(native,sprites["native-sentinel"]), "native icon changed during cleanup");
            NetworkRegistration.Register();
        });
    }
}
