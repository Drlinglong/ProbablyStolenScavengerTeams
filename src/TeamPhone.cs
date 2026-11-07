using System;
using HarmonyLib;

namespace ScavengerTeams
{
    internal static class TeamPhone
    {
        internal const long Number = 72283;
        internal const string DialogKey = ModEntry.Id + ".supply_phone";
        private static readonly Func<StorePhoneClient, Dialogue> Handler = Build;
        private static bool registered;
        internal static void Register()
        {
            try
            {
                var contacts = PlayerStore.Instance.PhoneClientDict;
                if (contacts.TryGetValue(Number, out var existing) && existing.dialogFuncId != DialogKey)
                    throw new InvalidOperationException("Supply phone number is already owned by another contact.");
                PhoneClientList.DialogFuncs[DialogKey] = Handler;
                contacts[Number] = new StorePhoneClient { displayName = Text.Get("phone.contact_name"),
                    locID = DialogKey, dialogFuncId = DialogKey, dialedBefore = true,
                    phoneState = StorePhoneClient.PhoneState.Regular };
                registered = true;
            }
            catch (Exception error) { ModEntry.Log.Error("Could not register supply phone: " + error.Message); }
        }
        internal static void Remove()
        {
            TeamPhoneLayout.Remove();
            if (PhoneClientList.DialogFuncs.TryGetValue(DialogKey, out var handler) && handler == Handler)
                PhoneClientList.DialogFuncs.Remove(DialogKey);
            if (!registered) return; // Disable before a run has loaded must not construct PlayerStore.
            registered = false;
            var contacts = PlayerStore.Instance.PhoneClientDict;
            if (contacts.TryGetValue(Number, out var contact) && contact.dialogFuncId == DialogKey)
                contacts.Remove(Number);
        }
        private static Dialogue Reply(string key, params object[] args)
            => new Dialogue().SetText(Text.Get("phone.contact_name"), Text.Get(key, args))
                .SetEndAction(() => PhoneUIManager.Instance.OnPhoneDialogEnd());
        internal static Dialogue Build(StorePhoneClient contact)
        {
            var root = new Dialogue().SetText(Text.Get("phone.contact_name"), Text.Get("phone.greeting"));
            foreach (var team in TeamDefinitions.All) AddTeam(root, team);
            var hangup = Choice(Text.Get("phone.hangup"), Reply("phone.goodbye"));
            root.AddChoice(DirectoryMaster.Item("$"), Text.Get("phone.hangup"),
                choiceNextDialogue: hangup);
            return root;
        }
        private static Dialogue Choice(string label, Dialogue response)
        {
            // Phone UI renders Item2.plainText as the label, then advances to Item2.nextDialogue.
            var choice = new Dialogue().SetText("PLAYER", label);
            choice.nextDialogue = response;
            return choice;
        }
        private static void AddTeam(Dialogue root, TeamDefinition team)
        {
            var response = Reply("phone.unavailable");
            try
            {
                var state = TeamStateRepository.Read(team);
                var purchased = TeamStateRepository.GetLevel(team) > 0;
                var pause = !state.paused;
                var remaining = Math.Max(0, state.resumeAvailableDay - TeamStateRepository.Day);
                var key = !purchased ? "phone.locked_choice" : pause ? "phone.disable_choice"
                    : remaining > 0 ? "phone.cooldown_choice" : "phone.enable_choice";
                var label = Text.Get(key, team.MaterialName, remaining);
                root.AddChoice(DirectoryMaster.Item("$"), label,
                    choiceNextDialogue: Choice(label, response),
                    choiceAction: () => Change(team, pause, response),
                    choiceEnabled: () => CanChange(team, pause));
            }
            catch (Exception error)
            {
                ModEntry.Log.Error("Could not read supply phone option: " + error.Message);
                var label = Text.Get("phone.locked_choice", team.MaterialName);
                root.AddChoice(DirectoryMaster.Item("$"), label,
                    choiceNextDialogue: Choice(label, response), choiceEnabled: () => false);
            }
        }
        private static bool CanChange(TeamDefinition team, bool pause)
        {
            try
            {
                if (TeamStateRepository.GetLevel(team) == 0) return false;
                var state = TeamStateRepository.Read(team);
                return pause ? !state.paused : state.paused && TeamStateRepository.Day >= state.resumeAvailableDay;
            }
            catch { return false; }
        }
        private static void Change(TeamDefinition team, bool pause, Dialogue response)
        {
            var key = "phone.unavailable";
            try
            {
                if (TeamSupplyControl.SetPaused(team, pause)) key = pause ? "phone.pause_reply" : "phone.resume_reply";
            }
            catch (Exception error) { ModEntry.Log.Error("Could not change supply schedule: " + error.Message); }
            response.SetText(Text.Get("phone.contact_name"), Text.Get(key, team.MaterialName, DeliveryRules.Interval));
        }
    }

    [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName))]
    internal static class TeamPhoneName
    {
        private static bool Prefix(StorePhoneClient __instance, ref string __result)
        {
            if (__instance.dialogFuncId != TeamPhone.DialogKey) return true;
            __result = Text.Get("phone.contact_name");
            return false;
        }
    }
}
