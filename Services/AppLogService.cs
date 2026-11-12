using WalkieTalkie.Data;
using Microsoft.EntityFrameworkCore;

namespace WalkieTalkie.Services;

public class AppLogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<AppLogService> _logger;

    public AppLogService(IDbContextFactory<AppDbContext> dbFactory, ILogger<AppLogService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // Structured logging methods
    public async Task LogPlayerActionAsync(LogType logType, int playerId, string? details = null, int? groupId = null)
    {
        await LogStructuredAsync(logType, playerId, details, groupId);
    }

    public async Task LogSystemEventAsync(LogType logType, int adminPlayerId, string? details = null, int? groupId = null)
    {
        await LogStructuredAsync(logType, adminPlayerId, details, groupId);
    }

    private async Task LogStructuredAsync(LogType logType, int playerId, string? details = null, int? groupId = null)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.AppLogs.Add(new AppLog
            {
                Timestamp = DateTime.UtcNow,
                LogType = logType,
                PlayerId = playerId,
                GroupId = groupId,
                Details = details
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write structured app log: {LogType}, PlayerId: {PlayerId}",
                logType, playerId);
        }
    }

    // Backward compatibility method
    public async Task LogAsync(string message)
    {
        // For backward compatibility, treat this as a system event with admin ID (assuming admin is ID 1)
        await LogSystemEventAsync(LogType.SystemStartup, 1, message);
    }

    // Enhanced query methods
    public async Task<List<AppLog>> GetRecentLogsAsync(int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AppLogs
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve app logs");
            return new List<AppLog>();
        }
    }

    public async Task<List<AppLog>> GetLogsSinceAsync(DateTime since)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AppLogs
                .Where(x => x.Timestamp >= since)
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve app logs since {Since}", since);
            return new List<AppLog>();
        }
    }

    // New search methods
    public async Task<List<AppLog>> GetLogsByPlayerAsync(int playerId, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AppLogs
                .Where(x => x.PlayerId == playerId)
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve app logs for player {PlayerId}", playerId);
            return new List<AppLog>();
        }
    }

    public async Task<List<AppLog>> GetLogsByTypeAsync(LogType logType, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            return await db.AppLogs
                .Where(x => x.LogType == logType)
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve app logs for type {LogType}", logType);
            return new List<AppLog>();
        }
    }

    public async Task<List<AppLog>> SearchLogsAsync(int? playerId = null, int? groupId = null, LogType? logType = null, DateTime? startDate = null, DateTime? endDate = null, int count = 100)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var query = db.AppLogs.AsQueryable();

            if (playerId.HasValue)
            {
                query = query.Where(x => x.PlayerId == playerId.Value);
            }

            if (groupId.HasValue)
            {
                query = query.Where(x => x.GroupId == groupId.Value);
            }

            if (logType.HasValue)
            {
                query = query.Where(x => x.LogType == logType.Value);
            }

            if (startDate.HasValue)
            {
                query = query.Where(x => x.Timestamp >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.Timestamp <= endDate.Value);
            }

            return await query
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search app logs");
            return new List<AppLog>();
        }
    }

    public async Task ClearOldLogsAsync(int keepDays = 30)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var cutoff = DateTime.UtcNow.AddDays(-keepDays);
            var oldLogs = await db.AppLogs
                .Where(x => x.Timestamp < cutoff)
                .ToListAsync();

            if (oldLogs.Any())
            {
                db.AppLogs.RemoveRange(oldLogs);
                await db.SaveChangesAsync();
                _logger.LogInformation("Cleared {Count} old app logs older than {Days} days", oldLogs.Count, keepDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear old app logs");
        }
    }
}
