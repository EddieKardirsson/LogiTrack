using Microsoft.AspNetCore.Mvc;
using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using LogiTrack.Models;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemHealthController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SystemHealthController> _logger;

    public SystemHealthController(
        LogiTrackContext context, 
        IMemoryCache cache, 
        UserManager<ApplicationUser> userManager,
        ILogger<SystemHealthController> logger)
    {
        _context = context;
        _cache = cache;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<ActionResult> HealthCheck()
    {
        var stopwatch = Stopwatch.StartNew();
        var healthData = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
            Server = Environment.MachineName,
            Database = await CheckDatabaseHealth(),
            Cache = CheckCacheHealth(),
            Performance = await CheckPerformanceMetrics(),
            ResponseTimeMs = stopwatch.ElapsedMilliseconds
        };

        return Ok(healthData);
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult> SystemStatus()
    {
        var stopwatch = Stopwatch.StartNew();
        
        var status = new
        {
            SystemInfo = new
            {
                Uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime.ToUniversalTime()),
                ServerTime = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
            },
            DatabaseStats = await GetDatabaseStats(),
            CacheStats = GetCacheStats(),
            ActiveSessions = await GetActiveSessionCount(),
            MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024, // MB
            ResponseTimeMs = stopwatch.ElapsedMilliseconds
        };

        stopwatch.Stop();
        return Ok(status);
    }

    [HttpPost("clear-expired-sessions")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> ClearExpiredSessions()
    {
        var expiredSessions = await _context.UserSessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow || !s.IsActive)
            .ToListAsync();

        _context.UserSessions.RemoveRange(expiredSessions);
        await _context.SaveChangesAsync();

        return Ok(new { 
            Message = "Expired sessions cleared", 
            ClearedCount = expiredSessions.Count,
            Timestamp = DateTime.UtcNow 
        });
    }

    [HttpGet("database-integrity")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> CheckDatabaseIntegrity()
    {
        var issues = new List<string>();
        
        // Check for orphaned order items
        var orphanedOrderItems = await _context.OrderItems
            .Where(oi => !_context.Orders.Any(o => o.Items.Contains(oi)))
            .CountAsync();
        if (orphanedOrderItems > 0)
            issues.Add($"Found {orphanedOrderItems} orphaned order items");

        // Check for orders with invalid inventory references
        var invalidOrderItems = await _context.OrderItems
            .Where(oi => !_context.InventoryItems.Any(ii => ii.ItemId == oi.InventoryItemId))
            .CountAsync();
        if (invalidOrderItems > 0)
            issues.Add($"Found {invalidOrderItems} order items with invalid inventory references");

        // Check for users without roles
        var usersWithoutRoles = await _userManager.Users
            .Where(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id))
            .CountAsync();
        if (usersWithoutRoles > 0)
            issues.Add($"Found {usersWithoutRoles} users without assigned roles");

        return Ok(new
        {
            Status = issues.Count == 0 ? "Healthy" : "Issues Found",
            Issues = issues,
            CheckedAt = DateTime.UtcNow
        });
    }

    private async Task<object> CheckDatabaseHealth()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            var inventoryCount = await _context.InventoryItems.CountAsync();
            var ordersCount = await _context.Orders.CountAsync();
            var usersCount = await _context.Users.CountAsync();

            return new
            {
                Status = canConnect ? "Connected" : "Disconnected",
                InventoryItems = inventoryCount,
                Orders = ordersCount,
                Users = usersCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return new { Status = "Error", Message = ex.Message };
        }
    }

    private object CheckCacheHealth()
    {
        try
        {
            // Test cache functionality
            var testKey = "health_check_" + Guid.NewGuid();
            var testValue = DateTime.UtcNow;
            
            _cache.Set(testKey, testValue, TimeSpan.FromSeconds(10));
            var retrieved = _cache.Get(testKey);
            _cache.Remove(testKey);

            return new
            {
                Status = retrieved != null ? "Operational" : "Failed",
                TestPassed = retrieved?.Equals(testValue) ?? false
            };
        }
        catch (Exception ex)
        {
            return new { Status = "Error", Message = ex.Message };
        }
    }

    private async Task<object> CheckPerformanceMetrics()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Test database query performance
        await _context.InventoryItems.AsNoTracking().Take(1).ToListAsync();
        var dbQueryTime = stopwatch.ElapsedMilliseconds;
        
        stopwatch.Restart();
        // Test cache performance
        _cache.TryGetValue("test_key_" + Guid.NewGuid(), out _);
        var cacheQueryTime = stopwatch.ElapsedMilliseconds;
        
        return new
        {
            DatabaseQueryMs = dbQueryTime,
            CacheQueryMs = cacheQueryTime,
            Status = dbQueryTime < 100 && cacheQueryTime < 5 ? "Good" : "Slow"
        };
    }

    private async Task<object> GetDatabaseStats()
    {
        return new
        {
            TotalInventoryItems = await _context.InventoryItems.CountAsync(),
            TotalOrders = await _context.Orders.CountAsync(),
            TotalOrderItems = await _context.OrderItems.CountAsync(),
            TotalUsers = await _context.Users.CountAsync(),
            ActiveSessions = await _context.UserSessions.Where(s => s.IsActive).CountAsync()
        };
    }

    private object GetCacheStats()
    {
        // Note: IMemoryCache doesn't expose statistics directly
        // This is a simplified representation
        return new
        {
            Status = "Active",
            Note = "Cache statistics limited by IMemoryCache interface"
        };
    }

    private async Task<int> GetActiveSessionCount()
    {
        return await _context.UserSessions
            .Where(s => s.IsActive && s.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
    }
}
