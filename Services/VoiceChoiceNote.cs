using System.Text.Json;
using Microsoft.JSInterop;

namespace WalkieTalkie.Services;

/// <summary>
/// Chat line posted when a player saves a TTS voice, so each receiving browser can show which of its own voices
/// it will use for that player (browsers have different voices, e.g. Chrome lacks Edge's online Microsoft voices).
/// </summary>
public static class VoiceChoiceNote
{
    public const string Prefix = "[VOICE-CHOICE] ";

    public sealed record Choice(string? Key, string? Language, string? Region);

    public static string Create(string? key, string? language, string? region)
        => Prefix + JsonSerializer.Serialize(new Choice(Blank(key), Blank(language), Blank(region)));

    public static bool TryParse(string? text, out Choice choice)
    {
        choice = new Choice(null, null, null);
        if (text is null || !text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            choice = JsonSerializer.Deserialize<Choice>(text.Substring(Prefix.Length)) ?? choice;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string CacheKey(Choice c) => $"{c.Key}\n{c.Language}\n{c.Region}";

    /// <summary>The chosen voice as the sender's browser named it.</summary>
    public static string Describe(Choice c)
    {
        var name = RequestedName(c);
        if (name.Length == 0) name = "Default voice";

        var lang = c.Language ?? "";
        if (lang.Length > 0 && !string.IsNullOrEmpty(c.Region)) lang += "-" + c.Region.ToUpperInvariant();
        return lang.Length > 0 ? $"{name} [{lang}]" : name;
    }

    /// <summary>The requested voice's name, or empty for the default voice.</summary>
    public static string RequestedName(Choice c)
    {
        var key = c.Key ?? "";
        var name = key.StartsWith("name:", StringComparison.Ordinal) ? key.Substring(5).Split('|')[0]
            : key.StartsWith("uri:", StringComparison.Ordinal) ? key.Substring(4)
            : key;
        return name.Trim();
    }

    /// <summary>Describes the voice this browser uses (a trimmed <c>describeVoice</c> label) for a chosen voice.</summary>
    public static string DescribeLocalMatch(Choice c, string localLabel)
    {
        var requested = RequestedName(c);
        if (requested.Length == 0) return localLabel;
        if (localLabel == "Default voice") return "not available, using Default voice";

        // describeVoice labels are "Name (lang)".
        var localName = localLabel;
        var langStart = localLabel.LastIndexOf(" (", StringComparison.Ordinal);
        if (langStart > 0 && localLabel.EndsWith(')')) localName = localLabel.Substring(0, langStart);

        return string.Equals(localName.Trim(), requested, StringComparison.OrdinalIgnoreCase)
            ? $"available ({localLabel})"
            : $"not available, using {localLabel}";
    }

    /// <summary>Asks this browser which voice it would use for the choice, e.g. "not available, using X".</summary>
    public static async Task<string> ResolveLocalAsync(IJSRuntime js, Choice c)
    {
        await js.InvokeVoidAsync("GameVoice.getVoicesAsync");
        var label = await js.InvokeAsync<string>("GameVoice.describeVoice", c.Key ?? "", c.Language ?? "", c.Region ?? "");
        return DescribeLocalMatch(c, TrimVoiceLabel(label));
    }

    /// <summary>Shortens a <c>GameVoice.describeVoice</c> label for display.</summary>
    public static string TrimVoiceLabel(string? label)
    {
        label = (label ?? "").Trim();
        if (label.Length == 0 || label.StartsWith("Default voice", StringComparison.OrdinalIgnoreCase)) return "Default voice";
        var uri = label.IndexOf(" uri=", StringComparison.Ordinal);
        return uri > 0 ? label.Substring(0, uri) : label;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
