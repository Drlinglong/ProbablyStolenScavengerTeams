using System;
using HarmonyLib;

namespace ScavengerTeams
{
    public sealed class ModEntry : IMod
    {
        internal const string Id = "ps_scavenger_teams";
        internal const string DisplayName = "王尔德的拾荒者小队";
        internal static ModLog Log;
        private bool enabled;
        public void Init(ModManifest manifest) { Log = new ModLog(manifest); }
        public void OnEnable()
        {
            if (enabled) return;
            enabled = true;
            // RegisterModUpgrade touches the lazy PlayerStore singleton. It is unsafe at boot.
            ModHook.OnGameLoadedEarly += NetworkRegistration.Register;
            ModHook.OnNewGameStarted += RegisterNewRun;
            ModHook.OnModItemDirectoryInit += CrateItems.Register;
            ModHook.OnGenerateCustomerVeryLate += SafeQueue;
            ModHook.OnGameLoadedLate += DeliveryCoordinator.ClearRuntime;
            Log.Log("Four supply routes enabled. Services register after new-game/load initialization in the native scavenger category.");
        }
        public void OnDisable()
        {
            if (!enabled) return;
            enabled = false;
            ModHook.OnGameLoadedEarly -= NetworkRegistration.Register;
            ModHook.OnNewGameStarted -= RegisterNewRun;
            ModHook.OnModItemDirectoryInit -= CrateItems.Register;
            ModHook.OnGenerateCustomerVeryLate -= SafeQueue;
            ModHook.OnGameLoadedLate -= DeliveryCoordinator.ClearRuntime;
            ModHelper.RemoveNetworkUpgrades(Id);
            DeliveryCoordinator.ClearRuntime();
        }
        private static void RegisterNewRun(string scenario) => NetworkRegistration.Register();
        private static void SafeQueue(StoreClientManager manager)
        {
            try { DeliveryCoordinator.Queue(manager); }
            catch (Exception error) { Log.Error("Could not queue shipment: " + error.Message); }
        }
    }

    // The native button checks funds in the UI. Enforce the same invariant at invocation
    // for this supplier only, including hotkeys or another mod invoking the callback.
    [HarmonyPatch(typeof(NegociationUIManager), nameof(NegociationUIManager.OnWholesaleClick))]
    internal static class WholesaleGuard
    {
        private static bool executing;
        private static bool Prefix(ref bool __state)
        {
            var player = PlayerStore.Instance;
            if (!DeliveryCoordinator.IsSupplier(player?.currentClientInstance?.GetClientBlueprint())) return true;
            if (executing || !player.currentClientInstance.GetClientBlueprint().isIntroduced
                || !player.currentClientInstance.GetClientBlueprint().isOfferWholesale) return false;
            var price = player.GetWholesalePrice();
            if (price < 0 || player.playerCash < price) return false;
            executing = true;
            __state = true;
            return true;
        }
        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) executing = false;
            return __exception;
        }
    }
}
