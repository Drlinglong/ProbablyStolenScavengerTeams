using System;
using System.Linq;
using ScavengerTeams;

internal static partial class RegressionTests
{
    private static void BuyThrough(TeamDefinition team, int level)
    { for (var i = 1; i <= level; i++) BuyUpgrade(i, team); }

    private static StoreClient Visit(TeamDefinition team, int day)
    {
        StoreStation.Day = day;
        PlayerStore.Instance.currentClientInstance = null;
        EmporiumEntry.Instance = new EmporiumEntry();
        var manager = new StoreClientManager(); ModHook.Generate(manager);
        var client = manager.clientStack.Single(c => c.identifier == team.ClientId);
        PlayerStore.Instance.currentClientInstance = new StoreClientInstance(client);
        client.OnArrival(); client.mainDialogue.End();
        return client;
    }

    private static void RunAdditionalRoutes()
    {
        Check("all twelve native upgrade definitions have exact per-route prices and local prerequisites", () =>
        {
            var ordered = ModHelper.Upgrades.Keys.ToArray(); var index = 0;
            foreach (var team in TeamDefinitions.All) for (var level = 1; level <= 3; level++)
            {
                var def = ModHelper.Upgrades[team.UpgradeId(level)];
                var expected = team == TeamDefinitions.Electronic ? new[] { 250, 350, 500 } : new[] { 200, 300, 450 };
                Require(def.Cost == expected[level - 1], "wrong service fee");
                Require(ordered[index++] == team.UpgradeId(level), "route row order changed");
                Require(level == 1 ? def.Prerequisites == null
                    : def.Prerequisites.SequenceEqual(new[] { team.UpgradeId(level - 1) }), "cross-route prerequisite");
            }
        });
        foreach (var route in TeamDefinitions.All)
        {
            var team = route;
            for (var target = 1; target <= 3; target++)
            {
                var level = target;
                Check(team.Key + " level" + level + " exact stock, single supplier and first-offer discount", () =>
                {
                    BuyThrough(team, level); var client = Arrive(11);
                    var materialId = level == 1 ? team.MaterialId : team.CrateId;
                    Require(Stock.Count(i => i.identifier == materialId) == (level == 1 ? 6 : level == 2 ? 2 : 3), "wrong material stock");
                    Require(Stock.Count(i => i.identifier == "junk") == level * 2, "wrong junk stock");
                    Require(client.identifier == team.ClientId && client.wholesaleDiscount == -50, "wrong supplier/discount");
                    Require(TeamStateRepository.Read(team).nextDay == 14 && TeamStateRepository.Read(team).offerLevel == level, "wrong state route");
                    if (team == TeamDefinitions.Ore) Require(client.spriteName == "miner-test-double", "miner sprite not used");
                });
            }
            Check(team.Key + " crate inherits tags/value, opens into six correct units, blocks loose-item repair", () =>
            {
                var source = DirectoryMaster.Item(team.MaterialId);
                var crate = CrateItems.Create(team);
                Require(crate.unitValue == source.unitValue * 6 && crate.identifier == team.CrateId, "wrong crate identity/value");
                Require(source.Tags.Keys.All(crate.IsTag) && source.itemTypes.All(crate.itemTypes.Contains), "source tags/categories lost");
                Require(crate.onThisTargetItemFunc == null && !crate.mayThisTargetItemFunc(crate, source), "crate retained one-piece repair action");
                for (var y = 0; y < 3; y++) for (var x = 0; x < 3; x++) Require(crate.shape.GetLocal(x,y) == 1, "empty crate cell");
                GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, crate);
                UnpackController.Open(crate); UnpackController.Open(crate);
                var result = EmporiumEntry.Instance.invElement.Items;
                Require(result.Count == 6 && result.All(i => i.identifier == team.MaterialId && i.Owned), "wrong unpacked contents");
            });
            Check(team.Key + " failed unpack preserves crate and removes partial output", () =>
            {
                var crate = CrateItems.Create(team); var inv = EmporiumEntry.Instance.invElement;
                GraphUtils.TryAcceptAll(inv, crate);
                inv.Capacity = team == TeamDefinitions.Printer ? 17 : 14;
                EmporiumEntry.Instance.backInvinvElement.Capacity = 0;
                UnpackController.Open(crate);
                Require(inv.Items.Count == 1 && inv.Items[0] == crate && crate.unitCount == 1, "failed unpack lost or duplicated goods");
            });
            Check(team.Key + " uses wholesale funds and reentry guard", () =>
            {
                BuyUpgrade(1, team); Arrive(11);
                PlayerStore.Instance.playerCash = 0;
                Require(!Guard(out var rejected), "insufficient funds accepted"); ReleaseGuard(rejected);
                PlayerStore.Instance.playerCash = 10000;
                Require(Guard(out var accepted), "valid route wholesale rejected"); ReleaseGuard(accepted);
            });
        }
        Check("four due routes append exactly four customers in order and preserve native customers", () =>
        {
            foreach (var team in TeamDefinitions.All) BuyUpgrade(1, team);
            StoreStation.Day = 11;
            var manager = new StoreClientManager(); var native = new StoreClient { identifier = "native-client" };
            manager.AddClient(native); ModHook.Generate(manager); ModHook.Generate(manager);
            Require(manager.clientStack.Count == 5 && manager.clientStack[0] == native, "native client replaced or duplicate route");
            Require(manager.clientStack.Skip(1).Select(c => c.identifier).SequenceEqual(TeamDefinitions.All.Select(t => t.ClientId)), "wrong route ordering");
        });
        Check("route schedules, upgrades and first offers stay independent", () =>
        {
            BuyUpgrade(1); Visit(TeamDefinitions.Printer, 11).OnDismiss();
            StoreStation.Day = 12;
            BuyUpgrade(1, TeamDefinitions.Metal); BuyUpgrade(1, TeamDefinitions.Electronic); BuyUpgrade(1, TeamDefinitions.Ore);
            Require(TeamStateRepository.Read().nextDay == 14 && TeamStateRepository.Read(TeamDefinitions.Metal).nextDay == 13, "schedule leaked");
            var electronics = Visit(TeamDefinitions.Electronic, 13);
            Require(electronics.wholesaleDiscount == -50, "another route consumed electronics special");
            electronics.OnDismiss(); BuyUpgrade(2, TeamDefinitions.Electronic);
            Require(TeamStateRepository.Read(TeamDefinitions.Electronic).nextDay == 16, "upgrade reset electronics date");
            Require(Visit(TeamDefinitions.Printer,14).wholesaleDiscount == -15, "new route renewed printer special");
            Require(Visit(TeamDefinitions.Metal,14).wholesaleDiscount == -50, "printer consumed metal special");
            Require(TeamStateRepository.Read(TeamDefinitions.Ore).offerDay == -1, "unvisited ore state mutated");
        });
        Check("partial purchase/reload updates only the visited route and retains original printer save key", () =>
        {
            foreach (var team in TeamDefinitions.All) BuyUpgrade(1, team);
            var electronic = Visit(TeamDefinitions.Electronic,11); Purchase(electronic,Stock[0]);
            Require(TeamStateRepository.Read(TeamDefinitions.Electronic).soldMask == 1 && TeamStateRepository.Read().soldMask == 0, "cross-route purchase state");
            Require(PlayerStore.Instance.Data.Keys.Any(k => k.EndsWith("printer.delivery.v1")), "printer legacy state key changed");
            Reload(); Visit(TeamDefinitions.Electronic,11);
            Require(Stock.Count(i => i.identifier == "common_electronic") == 5 && Stock.Count == 7, "reload revived purchased electronics");
            Require(TeamStateRepository.Read(TeamDefinitions.Metal).offerDay == -1, "reload mutated metal history");
        });
        Check("new route overflow keeps crates first and never regenerates discarded junk", () =>
        {
            BuyThrough(TeamDefinitions.Ore,2); StoreStation.Day=11;
            EmporiumEntry.Instance.frontInvinvElement.Capacity=18;
            var manager=new StoreClientManager(); ModHook.Generate(manager);
            var client=manager.clientStack.Single(); PlayerStore.Instance.currentClientInstance=new StoreClientInstance(client);
            client.OnArrival(); client.mainDialogue.End();
            Require(Stock.Count==2 && Stock.All(i=>i.identifier==TeamDefinitions.Ore.CrateId), "ore junk displaced crates");
            Reload(); Visit(TeamDefinitions.Ore,11);
            Require(Stock.Count==2 && Stock.All(i=>i.identifier==TeamDefinitions.Ore.CrateId), "discarded ore junk reappeared");
        });
    }
}
