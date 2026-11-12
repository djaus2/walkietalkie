using WalkieTalkie.Data;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Services;

public sealed class UsableMsVoiceService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private List<UsableMsVoice> _voices = new();

    public UsableMsVoiceService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var voices = await db.UsableMsVoices
            .OrderBy(x => x.BrowserFamily)
            .ThenBy(x => x.VoiceName)
            .ThenBy(x => x.VoiceLang)
            .ToListAsync(cancellationToken);

        lock (_lock)
        {
            _voices = voices;
        }
    }

    public IReadOnlyList<UsableMsVoice> GetVoices(string browserFamily)
    {
        browserFamily = (browserFamily ?? string.Empty).Trim().ToLowerInvariant();
        lock (_lock)
        {
            return _voices
                .Where(x => string.Equals(x.BrowserFamily, browserFamily, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.VoiceName)
                .ThenBy(x => x.VoiceLang)
                .ToList();
        }
    }

    public IReadOnlyCollection<string> GetTokens(string browserFamily)
    {
        browserFamily = (browserFamily ?? string.Empty).Trim().ToLowerInvariant();
        lock (_lock)
        {
            return _voices
                .Where(x => string.Equals(x.BrowserFamily, browserFamily, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Token)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public async Task<int> ReplaceVoicesAsync(string browserFamily, IEnumerable<(string Token, string VoiceName, string? VoiceLang, string? VoiceUri)> voices, CancellationToken cancellationToken = default)
    {
        browserFamily = (browserFamily ?? string.Empty).Trim().ToLowerInvariant();
        var items = (voices ?? Array.Empty<(string Token, string VoiceName, string? VoiceLang, string? VoiceUri)>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Token))
            .Where(x => !string.IsNullOrWhiteSpace(x.VoiceName))
            .GroupBy(x => x.Token, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.VoiceName)
            .ThenBy(x => x.VoiceLang)
            .ToList();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.UsableMsVoices
            .Where(x => x.BrowserFamily == browserFamily)
            .ToListAsync(cancellationToken);

        db.UsableMsVoices.RemoveRange(existing);

        var now = DateTime.UtcNow;
        db.UsableMsVoices.AddRange(items.Select(x => new UsableMsVoice
        {
            BrowserFamily = browserFamily,
            Token = x.Token,
            VoiceName = x.VoiceName,
            VoiceLang = string.IsNullOrWhiteSpace(x.VoiceLang) ? null : x.VoiceLang.Trim(),
            VoiceUri = string.IsNullOrWhiteSpace(x.VoiceUri) ? null : x.VoiceUri.Trim(),
            UpdatedUtc = now
        }));

        await db.SaveChangesAsync(cancellationToken);
        await LoadAsync(cancellationToken);
        return items.Count;
    }
}
