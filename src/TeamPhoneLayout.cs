using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ScavengerTeams
{
    // The native phone scene has only three choice bubbles. Extend this contact to two columns.
    internal static class TeamPhoneLayout
    {
        private static DialoguePhoneUIManager owner;
        private static readonly List<DialogChoiceBubble> added = new List<DialogChoiceBubble>();
        private static RectTransform group;
        private static Vector2 groupSize, groupPosition;
        private static Vector2[] positions;

        internal static void Prepare(DialoguePhoneUIManager ui, bool supplyMenu)
        {
            if (!supplyMenu) { Restore(); return; }
            if (owner != ui) { Remove(); owner = ui; }
            var bubbles = ui.dialogChoiceBubbles;
            // Another UI mod may already provide six positions; preserve its layout.
            if (positions == null && bubbles.Count >= 6) return;
            if (positions == null)
            {
                positions = new Vector2[bubbles.Count];
                for (var i = 0; i < bubbles.Count; i++)
                    positions[i] = ((RectTransform)bubbles[i].transform).anchoredPosition;
                group = (RectTransform)bubbles[0].transform.parent;
                groupSize = group.sizeDelta;
                groupPosition = group.anchoredPosition;
                while (bubbles.Count < 6)
                {
                    var copy = Object.Instantiate(bubbles[0], group);
                    copy.gameObject.SetActive(false);
                    copy.index = bubbles.Count;
                    copy.type = 1;
                    bubbles.Add(copy);
                    added.Add(copy);
                }
            }
            var first = (RectTransform)bubbles[0].transform;
            var width = first.rect.width * first.localScale.x;
            var rowStep = positions.Length > 1 ? positions[0].y - positions[1].y
                : first.rect.height * first.localScale.y + 4;
            var columnStep = width + 12;
            var expandedWidth = Mathf.Max(groupSize.x, columnStep + width);
            group.sizeDelta = new Vector2(expandedWidth, groupSize.y);
            // Keep the original group's right edge fixed when making room for a second column.
            group.anchoredPosition = new Vector2(groupPosition.x - (expandedWidth - groupSize.x) * (1 - group.pivot.x), groupPosition.y);
            for (var i = 0; i < 6; i++)
                ((RectTransform)bubbles[i].transform).anchoredPosition =
                    new Vector2(positions[0].x + (i % 2) * columnStep, positions[0].y - (i / 2) * rowStep);
        }
        private static void Restore()
        {
            if (owner == null || group == null || positions == null) return;
            group.sizeDelta = groupSize;
            group.anchoredPosition = groupPosition;
            for (var i = 0; i < positions.Length; i++)
                ((RectTransform)owner.dialogChoiceBubbles[i].transform).anchoredPosition = positions[i];
            foreach (var bubble in added) if (bubble != null) bubble.gameObject.SetActive(false);
        }
        internal static void Remove()
        {
            Restore();
            foreach (var bubble in added)
            {
                if (owner != null) owner.dialogChoiceBubbles.Remove(bubble);
                if (bubble != null) Object.Destroy(bubble.gameObject);
            }
            added.Clear(); positions = null; group = null; owner = null;
        }
    }

    [HarmonyPatch(typeof(DialoguePhoneUIManager), nameof(DialoguePhoneUIManager.DisplayChoice))]
    internal static class TeamPhoneChoiceLayout
    {
        private static void Prefix(DialoguePhoneUIManager __instance)
        {
            var contact = PhoneUIManager.Instance.currentPhoneClient;
            TeamPhoneLayout.Prepare(__instance, contact?.dialogFuncId == TeamPhone.DialogKey
                && __instance.currentDialog?.choices.Count == 6);
        }
    }
}
