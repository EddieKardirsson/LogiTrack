using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Data;

public class LogiTrackContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=Data/logitrack.db");
}