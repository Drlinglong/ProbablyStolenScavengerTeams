namespace ScavengerTeams
{
    internal static class Text
    {
        internal static string Get(string key, params object[] args)
            => ModHelper.GetLocalized(ModEntry.Id, key, args);
        internal static string For(TeamDefinition team, string field, params object[] args)
            => Get("team." + team.Key + "." + field, args);
    }
}
