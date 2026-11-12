namespace WalkieTalkie.Data;

public enum LogType
{
    // Player actions
    PlayerLogin = 1,
    PlayerLogout = 2,
    PlayerPinChange = 3,
    PlayerTimeoutLogout = 4,

    // System events
    SystemStartup = 30,
    SystemShutdown = 31,
    Error = 32,
    Warning = 33,

    // Admin actions
    AdminAction = 40,
    UserManagement = 41,
    SettingsChanged = 42
}

public class AppLog
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public LogType LogType { get; set; }

    public int PlayerId { get; set; }  // Mandatory - player who triggered the action, or Admin ID for system events

    public int? GroupId { get; set; }  // Optional - group associated with the event

    public string? Details { get; set; }  // Optional - additional context or old message content

    // Computed property for backward compatibility
    public string Message
    {
        get
        {
            var parts = new List<string>();

            // Add log type description
            parts.Add($"[{LogType}]");

            // Add player name if available
            if (PlayerId > 0)
            {
                parts.Add($"Player {PlayerId}");
            }

            // Add group info if available
            if (GroupId.HasValue)
            {
                parts.Add($"Group {GroupId.Value}");
            }

            // Add details if available
            if (!string.IsNullOrWhiteSpace(Details))
            {
                parts.Add(Details);
            }

            return string.Join(" ", parts);
        }
        set => Details = value;  // For backward compatibility
    }
}
