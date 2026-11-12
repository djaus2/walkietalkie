using System;
using System.Collections.Generic;
using System.Linq;

namespace WalkieTalkie.Services;

public sealed class LobbyChatService
{
    private readonly object _lock = new();
    private readonly List<ChatMessage> _messages = new();
    private readonly Dictionary<int, HashSet<int>> _userDeletedMessages = new(); // userId -> set of message indexes

    public event Action? ChatUpdated;

    public sealed record ChatMessage(DateTime Utc, int SenderUserId, string SenderName, string Text, int? GroupId = null, int MessageIndex = 0);

    public IReadOnlyList<ChatMessage> GetMessages(int? userId = null, IEnumerable<int>? userGroupIds = null)
    {
        lock (_lock)
        {
            var messages = _messages.ToList();
            
            // Only show messages if user is in at least one group
            if (userGroupIds != null && !userGroupIds.Any())
            {
                // User not in any groups - no chat access
                return new List<ChatMessage>();
            }
            
            // Filter by group membership if provided
            if (userGroupIds != null)
            {
                var groupIds = userGroupIds.ToList();
                // Only show messages that are:
                // 1. Public messages (GroupId = null) from admins OR
                // 2. Messages from groups the user is actually a member of
                messages = messages.Where(m => 
                    (!m.GroupId.HasValue && m.SenderName.StartsWith("[ADMIN]")) || 
                    (m.GroupId.HasValue && groupIds.Contains(m.GroupId.Value))
                ).ToList();
            }
            
            // Filter out messages deleted by this user
            if (userId.HasValue)
            {
                if (_userDeletedMessages.TryGetValue(userId.Value, out var deletedIndexes))
                {
                    messages = messages.Where(m => !deletedIndexes.Contains(m.MessageIndex)).ToList();
                }
            }
            
            return messages;
        }
    }

    public bool AddMessage(int senderUserId, string senderName, string text, int? groupId = null)
    {
        if (senderUserId < 0) return false;

        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_lock)
        {
            var messageIndex = _messages.Count;
            _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, text, groupId, messageIndex));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
        return true;
    }

    public bool AddMessageWithGroupCheck(int senderUserId, string senderName, string text, IEnumerable<int> userGroupIds, int? groupId = null)
    {
        if (senderUserId < 0) return false;

        // Check if user is in any groups
        if (userGroupIds == null || !userGroupIds.Any())
        {
            return false; // User not in any groups - cannot send messages
        }

        // If groupId specified, check if user is member of that group
        if (groupId.HasValue && !userGroupIds.Contains(groupId.Value))
        {
            return false; // User not in specified group
        }

        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_lock)
        {
            var messageIndex = _messages.Count;
            _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, senderName ?? string.Empty, text, groupId, messageIndex));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
        return true;
    }

    public bool AddAdminBroadcast(int senderUserId, string senderName, string text)
    {
        if (senderUserId < 0) return false;

        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_lock)
        {
            var messageIndex = _messages.Count;
            // Admin broadcasts use groupId = null to make them visible to all users with group access
            _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, $"[ADMIN] {senderName ?? string.Empty}", text, null, messageIndex));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
        return true;
    }

    public bool AddAdminBroadcastWithGroupCheck(int senderUserId, string senderName, string text, IEnumerable<int> userGroupIds, bool isAdmin)
    {
        if (senderUserId < 0) return false;

        // Only admins can broadcast
        if (!isAdmin) return false;

        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;

        lock (_lock)
        {
            var messageIndex = _messages.Count;
            // Admin broadcasts use groupId = null to make them visible to all users with group access
            _messages.Add(new ChatMessage(DateTime.UtcNow, senderUserId, $"[ADMIN] {senderName ?? string.Empty}", text, null, messageIndex));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
        return true;
    }

    public void AddSystemMessage(string text, int? groupId = null)
    {
        text = (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        lock (_lock)
        {
            var messageIndex = _messages.Count;
            _messages.Add(new ChatMessage(DateTime.UtcNow, 0, "System", text, groupId, messageIndex));
            TrimIfNeeded();
        }

        ChatUpdated?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            _userDeletedMessages.Clear();
        }

        ChatUpdated?.Invoke();
    }

    public void DeleteMessageForUser(int userId, int messageIndex)
    {
        lock (_lock)
        {
            if (!_userDeletedMessages.ContainsKey(userId))
            {
                _userDeletedMessages[userId] = new HashSet<int>();
            }
            _userDeletedMessages[userId].Add(messageIndex);
        }

        ChatUpdated?.Invoke();
    }

    public void ClearChatForUser(int userId)
    {
        lock (_lock)
        {
            var allMessageIndexes = _messages.Select(m => m.MessageIndex).ToHashSet();
            _userDeletedMessages[userId] = allMessageIndexes;
        }

        ChatUpdated?.Invoke();
    }

    private void TrimIfNeeded()
    {
        const int max = 200;
        if (_messages.Count <= max) return;
        
        lock (_lock)
        {
            var removedCount = _messages.Count - max;
            _messages.RemoveRange(0, removedCount);
            
            // Update message indexes and clean up deleted message references
            for (int i = 0; i < _messages.Count; i++)
            {
                var message = _messages[i];
                _messages[i] = message with { MessageIndex = i };
            }
            
            // Clean up deleted message references that are out of range
            foreach (var deletedSet in _userDeletedMessages.Values)
            {
                deletedSet.RemoveWhere(index => index >= _messages.Count);
            }
        }
    }
}
