using Microsoft.AspNetCore.Identity;

namespace LogiTrack.Models;

public class ApplicationUser : IdentityUser
{
    // You can add additional properties here if needed
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}