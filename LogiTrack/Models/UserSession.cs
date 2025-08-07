using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

public class UserSession
{
    [Key]
    public int SessionId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string SessionToken { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Optional: Store session data as JSON
    public string? SessionData { get; set; }
    
    // Navigation property
    public ApplicationUser? User { get; set; }
}
