using System;

namespace WalkieTalkie.Services;

/// <summary>
/// Holds shared lobby voice state: the push-to-talk floor (who is currently
/// allowed to transmit) and the voice mute flag. One channel shared by all
/// logged-in users, walkie-talkie style.
/// </summary>
public sealed class VoiceLobbyService
{
    private readonly object _lock = new();

    public event Action? StateChanged;

    public int? VoiceFloorUserId { get; private set; }
    public string? VoiceFloorUserName { get; private set; }
    public DateTime? VoiceFloorUntilUtc { get; private set; }

    public bool VoiceMuted { get; private set; }
    public int? VoiceMutedByUserId { get; private set; }
    public string? VoiceMutedByUserName { get; private set; }

    private void OnStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch
        {
        }
    }

    public bool TryAcquireVoiceFloor(int userId, string userName, int holdSeconds = 15)
    {
        if (userId <= 0) return false;

        var now = DateTime.UtcNow;
        lock (_lock)
        {
            // Expire stale locks.
            if (VoiceFloorUntilUtc.HasValue && VoiceFloorUntilUtc.Value <= now)
            {
                VoiceFloorUserId = null;
                VoiceFloorUserName = null;
                VoiceFloorUntilUtc = null;
            }

            // Free.
            if (!VoiceFloorUserId.HasValue)
            {
                VoiceFloorUserId = userId;
                VoiceFloorUserName = userName ?? string.Empty;
                VoiceFloorUntilUtc = now.AddSeconds(Math.Max(3, holdSeconds));
                OnStateChanged();
                return true;
            }

            // Re-entrant for current holder (renew).
            if (VoiceFloorUserId.Value == userId)
            {
                VoiceFloorUserName = userName ?? string.Empty;
                VoiceFloorUntilUtc = now.AddSeconds(Math.Max(3, holdSeconds));
                OnStateChanged();
                return true;
            }

            return false;
        }
    }

    public bool ReleaseVoiceFloor(int userId)
    {
        if (userId <= 0) return false;

        lock (_lock)
        {
            if (VoiceFloorUserId != userId) return false;
            VoiceFloorUserId = null;
            VoiceFloorUserName = null;
            VoiceFloorUntilUtc = null;
        }

        OnStateChanged();
        return true;
    }

    public (bool ok, string? msg) ToggleVoiceMute(int userId, string userName)
    {
        if (userId <= 0) return (false, "Invalid user");

        lock (_lock)
        {
            if (VoiceMuted)
            {
                // Only the user who muted can unmute
                if (VoiceMutedByUserId != userId)
                {
                    return (false, $"Only {VoiceMutedByUserName ?? "the user who muted"} can unmute");
                }
                VoiceMuted = false;
                VoiceMutedByUserId = null;
                VoiceMutedByUserName = null;
            }
            else
            {
                VoiceMuted = true;
                VoiceMutedByUserId = userId;
                VoiceMutedByUserName = userName;
            }
        }

        OnStateChanged();
        return (true, null);
    }
}
