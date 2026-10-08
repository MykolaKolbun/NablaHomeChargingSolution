namespace EVHomeAPI.Models;

/// <summary>Registered app user. Passwords are stored only as BCrypt hashes.</summary>
public class User
{
    public int      Id           { get; set; }
    public string   Name         { get; set; } = string.Empty;
    public string   Email        { get; set; } = string.Empty;   // unique, stored lower-case
    public string   PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    public ICollection<StationAccess> StationAccesses { get; set; } = [];
}
