using System;
using System.Collections.Generic;
using System.Linq;

namespace ScavengerTeams
{
    internal sealed class TeamDefinition
    {
        internal readonly string Key, MaterialId, SpriteKey;
        internal readonly bool LooseOnly;
        internal string MaterialName => Text.For(this, "material");
        internal string VisitorName => Text.For(this, "visitor_name");
        private readonly int firstCost, secondCost, thirdCost;
        internal TeamDefinition(string key, string material, string sprite,
            int first = 200, int second = 300, int third = 450, bool looseOnly = false)
        {
            Key = key; MaterialId = material; SpriteKey = sprite;
            LooseOnly = looseOnly;
            firstCost = first; secondCost = second; thirdCost = third;
        }
        internal string CrateId => ModEntry.Id + "." + SpriteKey;
        internal string ClientId => ModEntry.Id + "." + Key + "_supplier";
        internal string StateKey => Key + ".delivery.v1";
        internal string UpgradeId(int level) => ModEntry.Id + "." + Key + "." + level;
        internal int Cost(int level) => level == 1 ? firstCost : level == 2 ? secondCost
            : level == 3 ? thirdCost : throw new ArgumentOutOfRangeException(nameof(level));
        internal string Title(int level) => Text.For(this, "service_title",
            level == 1 ? "I" : level == 2 ? "II" : level == 3 ? "III"
            : throw new ArgumentOutOfRangeException(nameof(level)));
    }

    internal static class TeamDefinitions
    {
        internal static readonly TeamDefinition Printer = new TeamDefinition("printer", "printer_plastic",
            "printer_crate");
        internal static readonly TeamDefinition Metal = new TeamDefinition("metal", "scrap_metal",
            "metal_crate");
        internal static readonly TeamDefinition Electronic = new TeamDefinition("electronic", "common_electronic",
            "electronic_crate", 250, 350, 500);
        internal static readonly TeamDefinition Ore = new TeamDefinition("ore", "common_ore",
            "ore_crate");
        internal static readonly TeamDefinition Bottle = new TeamDefinition("bottle", "wine_bottle",
            "bottle_icon", looseOnly: true);
        internal static readonly IReadOnlyList<TeamDefinition> Crated = Array.AsReadOnly(new[] { Printer, Metal, Electronic, Ore });
        internal static readonly IReadOnlyList<TeamDefinition> All = Array.AsReadOnly(new[] { Printer, Metal, Electronic, Ore, Bottle });
        internal static TeamDefinition ForCrate(string id) => Crated.FirstOrDefault(t => t.CrateId == id);
        internal static TeamDefinition ForClient(string id) => All.FirstOrDefault(t => t.ClientId == id);
    }
}
