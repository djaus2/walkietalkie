using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    [Required]
    public int MaxLoginHrs { get; set; } = 4;

    [Required]
    public bool AllowPlayerPinChange { get; set; } = true;

    // BCP-47 speech recognition language, e.g. "en-AU"; null uses the GameVoice default (en-AU).
    public string? SpeechRecognitionLanguage { get; set; }
}
