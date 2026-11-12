using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class UsableMsVoice
{
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string BrowserFamily { get; set; } = "chrome";

    [Required]
    [MaxLength(128)]
    public string Token { get; set; } = "";

    [Required]
    [MaxLength(256)]
    public string VoiceName { get; set; } = "";

    [MaxLength(32)]
    public string? VoiceLang { get; set; }

    [MaxLength(1024)]
    public string? VoiceUri { get; set; }

    [Required]
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
