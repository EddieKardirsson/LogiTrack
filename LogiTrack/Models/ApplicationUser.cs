using Microsoft.AspNetCore.Identity;

namespace LogiTrack.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties for session tracking
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    
    // Optional: Track user preferences for persistent state
    public string? Preferences { get; set; } // JSON string for user preferences
}