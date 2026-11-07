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
                    subtitleLabel: () => Text.Get("category.scavengers"),
                    prerequisites: level == 1 ? null : new[] { team.UpgradeId(level - 1) });
            }
        }
        internal static void RemoveRuntimeIcons() => SpriteDict.Instance?.spriteDictionary?.Remove(ModEntry.Id + ".bottle_icon");
        private static string Description(TeamDefinition team, int level)
        {
            return Text.For(team, "service_description." + level);
        }
        private static void OnUnlock(TeamDefinition team)
        {
            var state = TeamStateRepository.Read(team);
            DeliveryRules.Unlock(state, TeamStateRepository.Day);
            TeamStateRepository.Write(state, team);
        }
    }
}
