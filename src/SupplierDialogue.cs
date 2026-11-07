using System;

namespace ScavengerTeams
{
    internal static class SupplierDialogue
    {
        internal static void Configure(StoreClient client, string text, Action onFinished)
        {
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var current = client.mainDialogue;
            current.nextDialogue = null;
            current.SetEndAction(null);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0) current = current.NextDialogue();
                current.SetText(client.displayName, lines[i]);
            }
            current.SetEndAction(onFinished);
        }
    }
}
