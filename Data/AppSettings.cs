using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class AppSettings
{
    public int Id { get; set; } = 1;

    [Required]
    public int MaxLoginHrs { get; set; } = 4;

    [Required]
    public bool AllowPlayerPinChange { get; set; } = true;
}
