using System;
using System.Collections.Generic;

namespace ScavengerTeams
{
    internal static class UnpackController
    {
        private static readonly HashSet<GameItem> busy = new HashSet<GameItem>();
        internal static bool CanOpen(GameItem item) => item != null
            && TeamDefinitions.ForCrate(item.identifier) != null && item.unitCount == 1
            && item.parentInventory != null && GeneralHelper.IsItemEffectivelyOwned(item)
            && !busy.Contains(item) && !((item as GameItemElement)?.IsDestroyed() ?? false)
            && !GameMaster.current.InCombat();

        internal static void Open(GameItem crate)
        {
            if (!CanOpen(crate)) return;
            var team = TeamDefinitions.ForCrate(crate.identifier);
            busy.Add(crate);
            var created = new List<GameItem>();
            var stackFunctions = new List<Func<GameItem, GameItem, bool>>();
            var committed = false;
            try
            {
                // Keep the crate intact until ALL six units are placed. Stage without stacking
                // so a failed attempt can remove exactly these new objects, never existing stock.
                for (var i = 0; i < DeliveryRules.CrateContents; i++)
                {
                    var material = ItemSpawner.Spawn(team.MaterialId);
                    if (material == null) throw new InvalidOperationException("Missing material factory.");
                    created.Add(material);
                    stackFunctions.Add(material.mayStackItemFunc);
                    material.mayStackItemFunc = (a, b) => false;
                    GeneralHelper.SetItemOwned(material, true);
                    if (!TryPlace(crate.parentInventory, material))
                    {
                        return;
                    }
                }
                crate.Destroy();
                committed = true;
            }
            catch (Exception error)
            {
                ModEntry.Log.Error("Unpack failed: " + error.Message);
            }
            finally
            {
                for (var i = 0; i < created.Count; i++)
                {
                    if (!committed) created[i].Destroy();
                    else created[i].mayStackItemFunc = stackFunctions[i];
                }
                busy.Remove(crate);
            }
        }
        private static bool TryPlace(GameInventory parent, GameItem material)
        {
            var targets = new GraphNodeStorage[] { parent, EmporiumEntry.Instance.invElement,
                EmporiumEntry.Instance.backInvinvElement };
            foreach (var target in targets)
                if (target != null && GraphUtils.TryAcceptAll(target, material) == 0
                    && material.parentInventory != null) return true;
            return false;
        }
    }
}
