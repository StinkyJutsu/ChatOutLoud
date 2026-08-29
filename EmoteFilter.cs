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
            string text =
                messageElement.TryGetProperty(
                    "text",
                    out JsonElement textElement)
                    ? textElement.GetString() ?? ""
                    : "";

            return RemoveUnicodeEmoji(text);
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

        return RemoveUnicodeEmoji(
            filteredText.ToString());
    }

    private static string RemoveUnicodeEmoji(
        string text)
    {
        StringBuilder filteredText = new();

        System.Globalization.TextElementEnumerator elements =
            System.Globalization.StringInfo.GetTextElementEnumerator(
                text);

        while (elements.MoveNext())
        {
            string textElement =
                elements.GetTextElement();

            bool containsEmoji = false;

            foreach (Rune rune in
                     textElement.EnumerateRunes())
            {
                if (IsEmojiRune(rune.Value))
                {
                    containsEmoji = true;
                    break;
                }
            }

            if (!containsEmoji)
            {
                filteredText.Append(textElement);
            }
        }

        return filteredText.ToString();
    }

    private static bool IsEmojiRune(
        int value)
    {
        return
            value == 0x20E3 ||
            value == 0xFE0F ||
            value == 0x203C ||
            value == 0x2049 ||
            value == 0x2122 ||
            value == 0x2139 ||
            value == 0x24C2 ||
            value == 0x3030 ||
            value == 0x303D ||
            value == 0x3297 ||
            value == 0x3299 ||
            value == 0x2B50 ||
            value == 0x2B55 ||
            (value >= 0x2600 && value <= 0x27BF) ||
            (value >= 0x1F000 && value <= 0x1FAFF);
    }
}