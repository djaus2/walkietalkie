using WalkieTalkie.Data;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Services;

public sealed class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private int _maxLoginHrs = 4;
    private bool _allowPlayerPinChange = true;
    private string _speechRecognitionLanguage = DefaultSpeechRecognitionLanguage;
    private bool _loaded;

    public const string DefaultSpeechRecognitionLanguage = "en-AU";

    private static readonly System.Text.RegularExpressions.Regex LanguageTagPattern =
        new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public SettingsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public int MaxLoginHrs
    {
        get
        {
            lock (_lock)
            {
                return _maxLoginHrs;
            }
        }
    }

    public bool AllowPlayerPinChange
    {
        get
        {
            lock (_lock)
            {
                return _allowPlayerPinChange;
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        lock (_lock)
        {
            if (s is not null)
            {
                _maxLoginHrs = s.MaxLoginHrs;
                _allowPlayerPinChange = s.AllowPlayerPinChange;
                _speechRecognitionLanguage = string.IsNullOrWhiteSpace(s.SpeechRecognitionLanguage)
                    ? DefaultSpeechRecognitionLanguage
                    : s.SpeechRecognitionLanguage;
            }
            else
            {
                _maxLoginHrs = 4;
                _allowPlayerPinChange = true;
                _speechRecognitionLanguage = DefaultSpeechRecognitionLanguage;
            }
            _loaded = true;
        }
    }

    public async Task<int> GetMaxLoginHrsAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _maxLoginHrs;
        }
    }

    public async Task<bool> GetAllowPlayerPinChangeAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _allowPlayerPinChange;
        }
    }

    public async Task<string> GetSpeechRecognitionLanguageAsync(CancellationToken cancellationToken = default)
    {
        var needLoad = false;
        lock (_lock)
        {
            needLoad = !_loaded;
        }

        if (needLoad)
        {
            await LoadAsync(cancellationToken);
        }

        lock (_lock)
        {
            return _speechRecognitionLanguage;
        }
    }

    public static bool IsValidLanguageTag(string? value)
        => !string.IsNullOrWhiteSpace(value) && LanguageTagPattern.IsMatch(value.Trim());

    public async Task<bool> UpdateSpeechRecognitionLanguageAsync(string? newValue, CancellationToken cancellationToken = default)
    {
        var value = string.IsNullOrWhiteSpace(newValue) ? DefaultSpeechRecognitionLanguage : newValue.Trim();
        if (!IsValidLanguageTag(value)) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1 };
            db.Settings.Add(s);
        }

        s.SpeechRecognitionLanguage = value;
        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _speechRecognitionLanguage = value;
        }

        return true;
    }

    public async Task<bool> UpdateMaxLoginHrsAsync(int newValue, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1 };
            db.Settings.Add(s);
        }

        s.MaxLoginHrs = newValue;
        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _maxLoginHrs = newValue;
        }

        return true;
    }

    public async Task<bool> UpdateAllowPlayerPinChangeAsync(bool newValue, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var s = await db.Settings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (s is null)
        {
            s = new AppSettings { Id = 1 };
            db.Settings.Add(s);
        }

        s.AllowPlayerPinChange = newValue;
        await db.SaveChangesAsync(cancellationToken);

        lock (_lock)
        {
            _allowPlayerPinChange = newValue;
        }

        return true;
    }
}
