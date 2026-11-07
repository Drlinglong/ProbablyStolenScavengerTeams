namespace ScavengerTeams
{
    internal static class NetworkRegistration
    {
        internal static string UpgradeId(int level) => TeamDefinitions.Printer.UpgradeId(level);
        internal static void Register()
        {
            foreach (var route in TeamDefinitions.All) for (var i = 1; i <= 3; i++)
            {
                var team = route;
                var level = i;
                ModHelper.AddNetworkUpgrade(ModEntry.Id, team.UpgradeId(level), team.Cost(level),
                    "Scavengers", () => team.Title(level),
                    () => Description(team, level), () => OnUnlock(team),
                    iconKey: ModEntry.Id + ":" + team.SpriteKey,
                    subtitleLabel: () => "拾荒者",
                    prerequisites: level == 1 ? null : new[] { team.UpgradeId(level - 1) });
            }
        }
        private static string Description(TeamDefinition team, int level)
        {
            var cargo = level == 1 ? "6个" + team.MaterialName + "和2个垃圾"
                : DeliveryRules.MaterialCount(level) + "箱" + team.MaterialName + "和" + DeliveryRules.JunkCount(level) + "个垃圾";
            return "购买次日首次来访，此后每隔3天送来" + cargo
                + "。可按原价挑选购买，首次上门整批批发五折，此后八五折（均含剩余垃圾）。"
                + "每箱6个对应材料，占3×3格。升级只增加货物，不增加来客、不重置日期或首次特惠。服务费仅收一次，货款另付。";
        }
        private static void OnUnlock(TeamDefinition team)
        {
            var state = TeamStateRepository.Read(team);
            DeliveryRules.Unlock(state, TeamStateRepository.Day);
            TeamStateRepository.Write(state, team);
        }
    }
}
