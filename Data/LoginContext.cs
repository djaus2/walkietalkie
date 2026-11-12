using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class LoginContext
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string ContextId { get; set; } = "";

    public int UserId { get; set; }
    public AppUser? User { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
