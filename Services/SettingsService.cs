using WalkieTalkie.Data;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Services;

public sealed class SettingsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();
    private int _maxLoginHrs = 4;
    private bool _allowPlayerPinChange = true;
    private bool _loaded;

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
            }
            else
            {
                _maxLoginHrs = 4;
                _allowPlayerPinChange = true;
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
