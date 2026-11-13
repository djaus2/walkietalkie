using System;
using System.Collections.Generic;
using System.Linq;

namespace WalkieTalkie.Services;

/// <summary>
/// Holds lobby voice state per group: the push-to-talk floor (who is currently
/// allowed to transmit on a group channel) and the per-group voice mute flag.
/// A sender must hold the floor on ALL of their groups (their voice goes to all
/// of them); listeners are only affected through groups they share.
/// </summary>
public sealed class VoiceLobbyService
{
    private sealed class FloorState
    {
        public int UserId;
        public string UserName = string.Empty;
        public DateTime UntilUtc;
    }

    private sealed class MuteState
    {
        public int ByUserId;
        public string ByUserName = string.Empty;
    }

    private readonly object _lock = new();
    private readonly Dictionary<int, FloorState> _floors = new(); // groupId -> floor
    private readonly Dictionary<int, MuteState> _mutes = new();   // groupId -> mute

    public event Action? StateChanged;

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

    private void ExpireStale(DateTime now)
    {
        foreach (var gid in _floors.Where(kv => kv.Value.UntilUtc <= now).Select(kv => kv.Key).ToList())
        {
            _floors.Remove(gid);
        }
    }

    /// <summary>
    /// Atomically acquires (or renews) the voice floor on all of the user's
    /// groups. Fails if any of those groups' floors is held by another user.
    /// </summary>
    public bool TryAcquireVoiceFloor(int userId, string userName, IReadOnlyCollection<int> groupIds, int holdSeconds = 15)
    {
        if (userId <= 0) return false;
        if (groupIds is null || groupIds.Count == 0) return false;

        var now = DateTime.UtcNow;
        lock (_lock)
        {
            ExpireStale(now);

            foreach (var gid in groupIds)
            {
                if (_floors.TryGetValue(gid, out var f) && f.UserId != userId)
                {
                    return false;
                }
            }

            var until = now.AddSeconds(Math.Max(3, holdSeconds));
            foreach (var gid in groupIds)
            {
                if (_floors.TryGetValue(gid, out var f))
                {
                    f.UserName = userName ?? string.Empty;
                    f.UntilUtc = until;
                }
                else
                {
                    _floors[gid] = new FloorState { UserId = userId, UserName = userName ?? string.Empty, UntilUtc = until };
                }
            }
        }

        OnStateChanged();
        return true;
    }

    /// <summary>Releases the floor on the given groups wherever held by this user.</summary>
    public bool ReleaseVoiceFloor(int userId, IReadOnlyCollection<int> groupIds)
    {
        if (userId <= 0) return false;
        if (groupIds is null || groupIds.Count == 0) return false;

        var released = false;
        lock (_lock)
        {
            foreach (var gid in groupIds)
            {
                if (_floors.TryGetValue(gid, out var f) && f.UserId == userId)
                {
                    _floors.Remove(gid);
                    released = true;
                }
            }
        }

        if (released) OnStateChanged();
        return released;
    }

    /// <summary>Releases every group floor currently held by this user.</summary>
    public bool ReleaseAllVoiceFloors(int userId)
    {
        if (userId <= 0) return false;

        var released = false;
        lock (_lock)
        {
            foreach (var gid in _floors.Where(kv => kv.Value.UserId == userId).Select(kv => kv.Key).ToList())
            {
                _floors.Remove(gid);
                released = true;
            }
        }

        if (released) OnStateChanged();
        return released;
    }

    /// <summary>Returns the floor holder on any of the given groups, if held by someone else.</summary>
    public (int UserId, string UserName)? GetFloorHolder(IEnumerable<int> groupIds, int excludeUserId)
    {
        if (groupIds is null) return null;

        var now = DateTime.UtcNow;
        lock (_lock)
        {
            ExpireStale(now);
            foreach (var gid in groupIds)
            {
                if (_floors.TryGetValue(gid, out var f) && f.UserId != excludeUserId)
                {
                    return (f.UserId, f.UserName);
                }
            }
        }

        return null;
    }

    public bool IsFloorHeldByOther(IEnumerable<int> groupIds, int userId)
        => GetFloorHolder(groupIds, userId).HasValue;

    /// <summary>True if a specific group's voice channel is muted.</summary>
    public bool IsGroupMuted(int? groupId)
    {
        if (!groupId.HasValue) return false;
        lock (_lock)
        {
            return _mutes.ContainsKey(groupId.Value);
        }
    }

    /// <summary>True if ANY of the given groups is muted.</summary>
    public bool IsMuted(IEnumerable<int> groupIds)
    {
        if (groupIds is null) return false;
        lock (_lock)
        {
            foreach (var gid in groupIds)
            {
                if (_mutes.ContainsKey(gid)) return true;
            }
        }
        return false;
    }

    /// <summary>Returns mute info for any of the given groups, if muted.</summary>
    public (int ByUserId, string ByUserName)? GetMuteInfo(IEnumerable<int> groupIds)
    {
        if (groupIds is null) return null;
        lock (_lock)
        {
            foreach (var gid in groupIds)
            {
                if (_mutes.TryGetValue(gid, out var m))
                {
                    return (m.ByUserId, m.ByUserName);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Mutes all of the user's groups, or unmutes them if any are muted.
    /// Only the user who muted can unmute.
    /// </summary>
    public (bool ok, string? msg) ToggleVoiceMute(int userId, string userName, IReadOnlyCollection<int> groupIds)
    {
        if (userId <= 0) return (false, "Invalid user");
        if (groupIds is null || groupIds.Count == 0) return (false, "Not a member of any groups");

        lock (_lock)
        {
            var mutedBy = new HashSet<int>();
            string? mutedByName = null;
            foreach (var gid in groupIds)
            {
                if (_mutes.TryGetValue(gid, out var m))
                {
                    mutedBy.Add(m.ByUserId);
                    mutedByName ??= m.ByUserName;
                }
            }

            if (mutedBy.Count > 0)
            {
                if (!(mutedBy.Count == 1 && mutedBy.Contains(userId)))
                {
                    return (false, $"Only {mutedByName ?? "the user who muted"} can unmute");
                }

                foreach (var gid in groupIds)
                {
                    _mutes.Remove(gid);
                }
            }
            else
            {
                foreach (var gid in groupIds)
                {
                    _mutes[gid] = new MuteState { ByUserId = userId, ByUserName = userName ?? string.Empty };
                }
            }
        }

        OnStateChanged();
        return (true, null);
    }
}
