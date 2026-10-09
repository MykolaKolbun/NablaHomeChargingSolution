namespace EVHomeAPI.Models;

/// <summary>FCM registration token of one app install (a user may have several devices).</summary>
public class DeviceToken
{
    public int      Id         { get; set; }
    public int      UserId     { get; set; }
    public string   Token      { get; set; } = string.Empty;   // unique
    public string   Platform   { get; set; } = "android";
    /// <summary>App language at registration ("uk" | "en") — push texts are rendered server-side.</summary>
    public string   Language   { get; set; } = "uk";
    public DateTime CreatedAt  { get; set; }
    public DateTime LastSeenAt { get; set; }

    public User User { get; set; } = null!;
}
