using System;
using System.Linq;

namespace ScavengerTeams
{
    internal static class DeliveryCoordinator
    {
        internal const string ClientId = ModEntry.Id + ".printer_supplier";
        private const string StockIndexTag = "PS_SCAVENGER_STOCK_INDEX";
        private static StoreClient activeClient;

        internal static void Queue(StoreClientManager manager)
        {
            if (PlayerStore.Instance == null || StoreStation.Instance == null || manager == null) return;
            foreach (var team in TeamDefinitions.All)
            {
                try { QueueTeam(manager, team); }
                catch (Exception error) { ModEntry.Log.Error(team.Key + " shipment could not queue: " + error.Message); }
            }
        }
        private static void QueueTeam(StoreClientManager manager, TeamDefinition team)
        {
            var level = TeamStateRepository.GetLevel(team);
            if (level == 0) return;
            var state = TeamStateRepository.Read(team);
            // Recover installations into an existing unlocked run without resetting later upgrades.
            if (state.nextDay < 0)
            {
                DeliveryRules.Unlock(state, TeamStateRepository.Day);
                TeamStateRepository.Write(state, team);
            }
            if (!DeliveryRules.CanQueue(state, TeamStateRepository.Day, level)
                || manager.clientStack.Any(c => c?.identifier == team.ClientId)
                || PlayerStore.Instance.currentClientInstance?.GetClientBlueprint()?.identifier == team.ClientId) return;
            manager.AddClient(Create(team));
        }
        internal static bool IsSupplier(StoreClient client) => TeamDefinitions.ForClient(client?.identifier) != null;
        internal static void ClearRuntime() { activeClient = null; }

        private static StoreClient Create(TeamDefinition team)
        {
            var client = new StoreClient
            {
                identifier = team.ClientId,
                displayName = team.VisitorName,
                spriteName = team == TeamDefinitions.Ore ? SpriteDict.GetRandomMinerSpriteName() : SpriteDict.GetRandomScavSpriteName(),
                clientFaction = "FACTION_LOWER_LEVEL",
                clientIntent = StoreClient.ClientIntent.SELL,
                dismissable = true, arrestable = false, nonShootable = true,
                CanReturn = false, CanReturnAsBuyer = false
            };
            client.SetWholesale(-15);
            client.AddBasicDialog();
            client.OnArrival = () => Arrive(client, team);
            client.OnDismiss = () => Close(client, team);
            // Native OnItemBought calls the supplier's onItemSold before ownership is transferred.
            client.onItemSold = (item, price) => MarkSold(client, item, team);
            client.mainDialogue.SetText(client.displayName, Greeting(team, 0))
                .SetEndAction(() => Introduce(client, team));
            ModHelper.SetClientSource(client, ModEntry.Id);
            client.CompleteClientCreation();
            return client;
        }
        private static void Arrive(StoreClient client, TeamDefinition team)
        {
            activeClient = client;
            var state = TeamStateRepository.Read(team);
            DeliveryRules.Arrive(state, TeamStateRepository.Day, TeamStateRepository.GetLevel(team));
            TeamStateRepository.Write(state, team);
            client.SetWholesale(DeliveryRules.WholesaleDiscount(state));
            SupplierDialogue.Configure(client, Greeting(team, state.offerGreetingLevel), () => Introduce(client, team));
        }
        private static string Greeting(TeamDefinition team, int level)
        {
            var prefix = team == TeamDefinitions.Ore ? "team.ore." : "team.";
            return Text.Get(prefix + (level > 0 ? "intro." + level : "greeting"));
        }
        private static void Introduce(StoreClient client, TeamDefinition team)
        {
            if (client.isIntroduced || !ReferenceEquals(activeClient, client)) return;
            var state = TeamStateRepository.Read(team);
            try
            {
                for (var i = 0; i < DeliveryRules.StockCount(state.offerLevel, team.LooseOnly); i++)
                {
                    if (DeliveryRules.IsSold(state, i)) continue;
                    var index = i;
                    // Always place all useful materials/crates before any junk. Native overflow
                    // destroys the item it cannot place; do not roll back or offer a retry.
                    var material = i < DeliveryRules.MaterialCount(state.offerLevel, team.LooseOnly);
                    var id = material ? (state.offerLevel == 1 || team.LooseOnly ? team.MaterialId : team.CrateId) : "junk";
                    var item = ItemSpawner.Spawn(id);
                    if (item == null) throw new InvalidOperationException("Missing stock: " + id);
                    item.mayStackItemFunc = (a, b) => false;
                    item.ModifyTag(StockIndexTag, tag => tag.Enable().SetInt(index));
                    if (!PlayerStore.Instance.TryAddDirectSellingItemToTable(item))
                        DeliveryRules.Discard(state, i, team.LooseOnly);
                }
            }
            catch (Exception error)
            {
                ModEntry.Log.Error("Could not finish placing shipment: " + error.Message);
            }
            finally
            {
                if (state.soldMask == (1 << DeliveryRules.StockCount(state.offerLevel, team.LooseOnly)) - 1) state.closed = true;
                TeamStateRepository.Write(state, team);
                client.OnIntroduced();
            }
        }
        private static void MarkSold(StoreClient client, GameItem item, TeamDefinition team)
        {
            if (!ReferenceEquals(activeClient, client) || !item.IsTag(StockIndexTag)) return;
            var state = TeamStateRepository.Read(team);
            DeliveryRules.Sold(state, item.GetTagReadonly(StockIndexTag).GetInt(), team.LooseOnly);
            if (state.soldMask == (1 << DeliveryRules.StockCount(state.offerLevel, team.LooseOnly)) - 1) state.closed = true;
            TeamStateRepository.Write(state, team);
        }
        private static void Close(StoreClient client, TeamDefinition team)
        {
            if (!ReferenceEquals(activeClient, client)) return;
            var state = TeamStateRepository.Read(team);
            state.closed = true;
            TeamStateRepository.Write(state, team);
            activeClient = null;
        }
    }
}
