using System.Text.Json.Serialization;

namespace WalkieTalkie.Data;

public class AuthExport
{
    [JsonPropertyName("exportDate")]
    public DateTime ExportDate { get; set; }
    
    [JsonPropertyName("users")]
    public List<AuthUser> Users { get; set; } = new();
    
    [JsonPropertyName("groups")]
    public List<AuthGroup> Groups { get; set; } = new();
}

public class AuthUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("roles")]
    public string Roles { get; set; } = string.Empty;
    
    [JsonPropertyName("pin")]
    public string Pin { get; set; } = string.Empty;
    
    [JsonPropertyName("groups")]
    public List<string> Groups { get; set; } = new();
}

public class AuthGroup
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("ownerId")]
    public int OwnerId { get; set; }
    
    [JsonPropertyName("members")]
    public List<string> Members { get; set; } = new();
}
