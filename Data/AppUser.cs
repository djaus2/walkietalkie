using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class AppUser
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(512)]
    public string Roles { get; set; } = "";

    [Required]
    public byte[] PinSalt { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PinHash { get; set; } = Array.Empty<byte>();

    [MaxLength(1024)]
    public string? PreferredTtsVoice { get; set; }

    [MaxLength(16)]
    public string? PreferredTtsLanguage { get; set; }

    [MaxLength(16)]
    public string? PreferredTtsRegion { get; set; }

    [MaxLength(1024)]
    public string? AdminDesktopFallbackTtsVoice { get; set; }

    [MaxLength(16)]
    public string? AdminDesktopFallbackTtsLanguage { get; set; }

    [MaxLength(16)]
    public string? AdminDesktopFallbackTtsRegion { get; set; }

    [MaxLength(64)]
    public string? ActiveSessionId { get; set; }

    [Required]
    public bool IsLoggedIn { get; set; } = false;
}
