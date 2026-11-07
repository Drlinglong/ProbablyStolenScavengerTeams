using System;
using System.Collections.Generic;

namespace ScavengerTeams
{
    internal static class CrateItems
    {
        internal const string MaterialId = "printer_plastic";
        internal const string CrateId = ModEntry.Id + ".printer_crate";
        private static string Description(TeamDefinition team) => Text.For(team, "crate_description", DeliveryRules.CrateContents);
        internal static GridShapeBuilder FilledShape() => new GridShapeBuilder(3, 3).SetDataFill(3, 3);
        internal static void Register(ModItemDirectory directory)
        {
            foreach (var route in TeamDefinitions.All)
            {
                var team = route;
                directory.Add(team.CrateId, () => Create(team));
            }
        }
        internal static GameItem Create(TeamDefinition team = null)
        {
            team = team ?? TeamDefinitions.Printer;
            // Start from the original factory: preserve its full tag state and item categories.
            var crate = DirectoryMaster.Item(team.MaterialId);
            if (crate == null) throw new InvalidOperationException(team.MaterialId + " is not registered.");
            crate.identifier = team.CrateId;
            crate.name = Text.For(team, "crate_name");
            crate.shortDescription = Description(team);
            crate.flavorText = Text.For(team, "crate_flavor");
            crate.unitValue = checked(crate.unitValue * DeliveryRules.CrateContents);
            crate.SetSpriteAndShapeFromMod(ModEntry.Id, team.SpriteKey);
            crate.SetShape(FilledShape()); // Size alone creates an EMPTY mask in the native API.
            crate.mayStackItemFunc = (a, b) => false;
            // Inherit tags/categories, not the loose material's single-piece repair/target actions.
            crate.maySelectSlotItemFunc = (item, inv, slot) => false;
            crate.canSelectSlotItemFunc = (item, inv, slot) => false;
            crate.mayThisTargetItemFunc = (source, target) => false;
            crate.canThisTargetItemFunc = (source, target) => false;
            crate.onThisTargetItemFunc = null;
            crate.mayActivateSlotItemFunc = (item, inv, slot) => UnpackController.CanOpen(item);
            crate.canActivateSlotItemFunc = (item, inv, slot) => UnpackController.CanOpen(item);
            crate.onActivateSlotItemFunc = (item, inv, slot) => UnpackController.Open(item);
            crate.onLoaded += item => RestoreShape(item, team);
            ModHelper.SetSource(crate, ModEntry.Id, ModEntry.DisplayName);
            return crate;
        }
        private static void RestoreShape(GameItem item, TeamDefinition team)
        {
            // Saved item data overrides factory shape/text, including the old empty 3x3 mask.
            // Retain its saved position/orientation while filling all nine local cells.
            item.SetShape(new GridShapeBuilder(item.shape).SetDataFill(3, 3));
            item.shortDescription = Description(team);
            item.name = Text.For(team, "crate_name");
            item.flavorText = Text.For(team, "crate_flavor");
        }
    }
}
