namespace WalkieTalkie.Services;

public static class VoiceCatalog
{
    public static string NormalizeToken(string? voiceName, string? voiceLang)
    {
        var name = (voiceName ?? string.Empty).Trim().ToLowerInvariant();
        var lang = (voiceLang ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');

        static string Slug(string input)
        {
            var chars = input
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var value = new string(chars);
            while (value.Contains("__", StringComparison.Ordinal))
            {
                value = value.Replace("__", "_", StringComparison.Ordinal);
            }

            return value.Trim('_');
        }

        var namePart = Slug(name);
        var langPart = Slug(lang);

        if (string.IsNullOrWhiteSpace(namePart) && string.IsNullOrWhiteSpace(langPart)) return string.Empty;
        if (string.IsNullOrWhiteSpace(langPart)) return namePart;
        if (string.IsNullOrWhiteSpace(namePart)) return langPart;
        return $"{namePart}_{langPart}";
    }

    public static string BuildCanonicalVoiceKey(string? voiceName, string? voiceLang)
    {
        var name = (voiceName ?? string.Empty).Trim();
        var lang = (voiceLang ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return string.IsNullOrWhiteSpace(lang)
            ? $"name:{name}"
            : $"name:{name}|lang:{lang}";
    }
}
