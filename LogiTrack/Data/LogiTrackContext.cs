using LogiTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

public class LogiTrackContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=Data/logitrack.db");
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Call the base method to configure Identity tables
        base.OnModelCreating(modelBuilder);
        
        // Configure OrderItem -> InventoryItem relationship
        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.InventoryItem)
            .WithMany()
            .HasForeignKey(oi => oi.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent deleting inventory items that are in orders
        
        // Configure Order -> OrderItems relationship with cascade delete
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);  // When an order is deleted, its order items are also deleted
    }
}