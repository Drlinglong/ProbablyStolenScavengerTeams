using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ScavengerTeams
{
    // Use a separate set of native-looking bubbles: native fit-to-text controls cannot form fixed columns.
    internal static class TeamPhoneLayout
    {
        private static DialoguePhoneUIManager owner;
        private static List<DialogChoiceBubble> native;
        private static readonly List<DialogChoiceBubble> choices = new List<DialogChoiceBubble>();
        private static RectTransform panel;

        internal static void Prepare(DialoguePhoneUIManager ui, bool supplyMenu)
        {
            if (!supplyMenu) { Restore(); return; }
            if (owner != ui) { Remove(); owner = ui; }
            if (panel == null) Create(ui);
            Position(); // Finish layout before replacing the working native list.
            foreach (var bubble in native) bubble.gameObject.SetActive(false);
            ui.dialogChoiceBubbles = choices;
            panel.gameObject.SetActive(true);
        }
        private static void Create(DialoguePhoneUIManager ui)
        {
            native = ui.dialogChoiceBubbles;
            var template = native[0];
            var group = (RectTransform)template.transform.parent;
            panel = (RectTransform)new GameObject("ScavengerPhoneChoices", typeof(RectTransform)).transform;
            panel.SetParent(group.parent, false); // Retain native phone visibility/dragging hierarchy.
            panel.anchorMin = panel.anchorMax = ((RectTransform)group.parent).pivot;
            panel.pivot = new Vector2(1, 1);
            for (var i = 0; i < 6; i++)
            {
                var bubble = Object.Instantiate(template, panel);
                bubble.index = i;
                bubble.type = 1;
                // The prefab's preferred-width fitter was the cause of the overlapping columns.
                foreach (var fitter in bubble.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
                foreach (var layout in bubble.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
                var rect = (RectTransform)bubble.transform;
                rect.localScale = new Vector3(1, 1, 1);
                rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                var text = bubble.TMP;
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize * template.transform.localScale.y;
                text.fontSizeMin = text.fontSizeMax * 0.7f;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                var textRect = text.rectTransform;
                textRect.anchorMin = new Vector2(0, 0);
                textRect.anchorMax = new Vector2(1, 1);
                textRect.offsetMin = new Vector2(18, 6);
                textRect.offsetMax = new Vector2(-18, -6);
                bubble.gameObject.SetActive(false);
                choices.Add(bubble);
            }
        }
        private static void Position()
        {
            // The manager is outside the UI hierarchy. Only the actual panel is under Canvas.
            var canvas = panel.GetComponentInParent<Canvas>();
            var root = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (root == null)
            {
                // Keep a usable phone-local menu if another UI mod removes/reparents Canvas.
                var group = (RectTransform)native[0].transform.parent;
                Layout(520, 60, new Vector2(group.anchoredPosition.x + group.sizeDelta.x / 2,
                    group.anchoredPosition.y + 240));
                return;
            }
            var bounds = root.rect;
            var width = Mathf.Clamp(bounds.width * 0.42f, 280, 520);
            var row = Mathf.Min(68, (bounds.height - 140) / 6);
            // Canvas coordinates -> phone-local coordinates, so the menu stays inside the screen.
            var point = panel.parent.InverseTransformPoint(root.TransformPoint(
                new Vector3(bounds.xMax - 24, bounds.yMax - 60, 0)));
            Layout(width, row, new Vector2(point.x, point.y));
        }
        private static void Layout(float width, float row, Vector2 position)
        {
            panel.sizeDelta = new Vector2(width, row * 6 + 8);
            panel.anchoredPosition = position;
            for (var i = 0; i < 6; i++)
            {
                var rect = (RectTransform)choices[i].transform;
                rect.sizeDelta = new Vector2(width, row - 8);
                rect.anchoredPosition = new Vector2(0, -i * row - (i == 5 ? 8 : 0));
            }
        }
        private static void Restore()
        {
            if (owner != null && native != null && ReferenceEquals(owner.dialogChoiceBubbles, choices))
                owner.dialogChoiceBubbles = native;
            if (panel != null) panel.gameObject.SetActive(false);
            foreach (var bubble in choices) if (bubble != null) bubble.gameObject.SetActive(false);
        }
        internal static void Remove()
        {
            Restore();
            if (panel != null) Object.Destroy(panel.gameObject);
            choices.Clear(); native = null; panel = null; owner = null;
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
