using System;
using System.Collections.Generic;
using System.Linq;

namespace ScavengerTeams
{
    internal sealed class TeamDefinition
    {
        internal readonly string Key, MaterialId, SpriteKey, MaterialName, ServiceName, VisitorName;
        private readonly int firstCost, secondCost, thirdCost;
        internal TeamDefinition(string key, string material, string sprite, string name,
            string service, string visitor, int first = 200, int second = 300, int third = 450)
        {
            Key = key; MaterialId = material; SpriteKey = sprite; MaterialName = name;
            ServiceName = service; VisitorName = visitor;
            firstCost = first; secondCost = second; thirdCost = third;
        }
        internal string CrateId => ModEntry.Id + "." + SpriteKey;
        internal string ClientId => ModEntry.Id + "." + Key + "_supplier";
        internal string StateKey => Key + ".delivery.v1";
        internal string UpgradeId(int level) => ModEntry.Id + "." + Key + "." + level;
        internal int Cost(int level) => level == 1 ? firstCost : level == 2 ? secondCost
            : level == 3 ? thirdCost : throw new ArgumentOutOfRangeException(nameof(level));
        internal string Title(int level) => ServiceName + "，" + level + "级）";
    }

    internal static class TeamDefinitions
    {
        internal static readonly TeamDefinition Printer = new TeamDefinition("printer", "printer_plastic",
            "printer_crate", "3D打印耗材", "拾荒者小队（打印耗材", "打印耗材拾荒者");
        internal static readonly TeamDefinition Metal = new TeamDefinition("metal", "scrap_metal",
            "metal_crate", "废金属", "拾荒者小队（废金属", "废金属拾荒者");
        internal static readonly TeamDefinition Electronic = new TeamDefinition("electronic", "common_electronic",
            "electronic_crate", "电子元件（普通）", "拾荒者小队（电子元件", "电子元件拾荒者", 250, 350, 500);
        internal static readonly TeamDefinition Ore = new TeamDefinition("ore", "common_ore",
            "ore_crate", "矿石", "小行星开采队（矿石", "小行星矿工");
        internal static readonly IReadOnlyList<TeamDefinition> All = Array.AsReadOnly(new[] { Printer, Metal, Electronic, Ore });
        internal static TeamDefinition ForCrate(string id) => All.FirstOrDefault(t => t.CrateId == id);
        internal static TeamDefinition ForClient(string id) => All.FirstOrDefault(t => t.ClientId == id);
    }
}
