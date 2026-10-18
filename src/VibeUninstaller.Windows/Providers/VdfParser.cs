namespace VibeUninstaller.Windows.Providers;

/// <summary>Minimal Valve KeyValues (VDF/ACF) parser.</summary>
internal static class VdfParser
{
    public static Dictionary<string, object> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        var index = 0;
        return ParseObject(text, ref index, skipOpeningBrace: true);
    }

    private static Dictionary<string, object> ParseObject(string text, ref int index, bool skipOpeningBrace)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        SkipNoise(text, ref index);
        if (skipOpeningBrace && index < text.Length && text[index] == '{')
        {
            index++;
        }

        while (index < text.Length)
        {
            SkipNoise(text, ref index);
            if (index >= text.Length)
            {
                break;
            }

            if (text[index] == '}')
            {
                index++;
                break;
            }

            var key = ReadQuoted(text, ref index);
            if (key is null)
            {
                index++;
                continue;
            }

            SkipNoise(text, ref index);
            if (index < text.Length && text[index] == '{')
            {
                result[key] = ParseObject(text, ref index, skipOpeningBrace: true);
            }
            else
            {
                result[key] = ReadQuoted(text, ref index) ?? string.Empty;
            }
        }

        return result;
    }

    private static void SkipNoise(string text, ref int index)
    {
        while (index < text.Length)
        {
            var character = text[index];
            if (char.IsWhiteSpace(character) || character == '\0')
            {
                index++;
                continue;
            }

            if (character == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }

                continue;
            }

            return;
        }
    }

    private static string? ReadQuoted(string text, ref int index)
    {
        if (index >= text.Length || text[index] != '"')
        {
            return null;
        }

        index++;
        var builder = new System.Text.StringBuilder(32);
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '\\' && index + 1 < text.Length)
            {
                var next = text[index + 1];
                builder.Append(next switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    _ => next,
                });
                index += 2;
                continue;
            }

            if (character == '"')
            {
                index++;
                break;
            }

            builder.Append(character);
            index++;
        }

        return builder.ToString();
    }
}
