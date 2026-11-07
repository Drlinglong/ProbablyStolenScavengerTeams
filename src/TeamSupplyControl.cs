using System;

namespace ScavengerTeams
{
    internal static class TeamSupplyControl
    {
        internal static bool SetPaused(TeamDefinition team, bool paused)
        {
            if (TeamStateRepository.GetLevel(team) == 0) return false;
            var state = TeamStateRepository.Read(team);
            var changed = paused ? DeliveryRules.Pause(state, TeamStateRepository.Day)
                : DeliveryRules.Resume(state, TeamStateRepository.Day);
            if (!changed) return false;
            TeamStateRepository.Write(state, team);
            if (paused)
                PlayerStore.Instance.storeClientManager?.clientStack.RemoveAll(client => client.identifier == team.ClientId);
            return true;
        }
    }
}
