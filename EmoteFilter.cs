using System;
using System.Text;
using System.Text.Json;

namespace ChatOutLoud;

internal static class EmoteFilter
{
    public static string RemoveTwitchEmotes(
        JsonElement messageElement)
    {
        if (!messageElement.TryGetProperty(
                "fragments",
                out JsonElement fragments) ||
            fragments.ValueKind != JsonValueKind.Array)
        {
            return messageElement.TryGetProperty(
                    "text",
                    out JsonElement textElement)
                ? textElement.GetString() ?? ""
                : "";
        }

        StringBuilder filteredText = new();

        foreach (JsonElement fragment in
                 fragments.EnumerateArray())
        {
            string? fragmentType =
                fragment.TryGetProperty(
                    "type",
                    out JsonElement typeElement)
                    ? typeElement.GetString()
                    : null;

            bool isEmote =
                string.Equals(
                    fragmentType,
                    "emote",
                    StringComparison.Ordinal) ||
                string.Equals(
                    fragmentType,
                    "gif",
                    StringComparison.Ordinal) ||
                (fragment.TryGetProperty(
                     "emote",
                     out JsonElement emoteElement) &&
                 emoteElement.ValueKind == JsonValueKind.Object);

            if (isEmote)
            {
                continue;
            }

            if (fragment.TryGetProperty(
                    "text",
                    out JsonElement fragmentTextElement))
            {
                filteredText.Append(
                    fragmentTextElement.GetString());
            }
        }

        return filteredText.ToString();
    }
}