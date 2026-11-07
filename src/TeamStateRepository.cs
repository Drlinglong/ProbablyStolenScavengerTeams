using System;
using UnityEngine;

namespace ScavengerTeams
{
    internal static class TeamStateRepository
    {
        internal static DeliveryState Read(TeamDefinition team = null)
        {
            team = team ?? TeamDefinitions.Printer;
            var json = ModHelper.GetModData(ModEntry.Id, team.StateKey);
            if (string.IsNullOrEmpty(json)) return new DeliveryState();
            var state = JsonUtility.FromJson<DeliveryState>(json);
            if (!DeliveryRules.Valid(state))
                throw new InvalidOperationException("Invalid delivery state; refusing to reset it or grant duplicate stock.");
            return state;
        }
        internal static void Write(DeliveryState state, TeamDefinition team = null)
        {
            team = team ?? TeamDefinitions.Printer;
            if (!DeliveryRules.Valid(state)) throw new InvalidOperationException("Invalid delivery state.");
            ModHelper.SetModData(ModEntry.Id, team.StateKey, JsonUtility.ToJson(state));
        }
        internal static int Day => StoreStation.GetDayCounter();
        internal static int Level => GetLevel(TeamDefinitions.Printer);
        internal static int GetLevel(TeamDefinition team)
        {
            for (var level = 3; level >= 1; level--)
                if (ModHelper.IsNetworkUpgradeUnlocked(team.UpgradeId(level))) return level;
            return 0;
        }
    }
}
