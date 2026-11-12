using System.ComponentModel.DataAnnotations;

namespace WalkieTalkie.Data;

public sealed class Group
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = "";

    [MaxLength(256)]
    public string? Description { get; set; }

    public int OwnerUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public AppUser? OwnerUser { get; set; }
    public List<GroupMember> Members { get; set; } = new();
}

public sealed class GroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int UserId { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Group? Group { get; set; }
    public AppUser? User { get; set; }
}
