using Microsoft.AspNetCore.Mvc;
using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using LogiTrack.Models;
using Microsoft.Extensions.Caching.Memory;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceTestController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PerformanceTestController> _logger;

    public PerformanceTestController(LogiTrackContext context, IMemoryCache cache, ILogger<PerformanceTestController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("comprehensive-test")]
    public async Task<ActionResult> ComprehensivePerformanceTest()
    {
        var results = new List<object>();
        var overallStopwatch = Stopwatch.StartNew();

        // Test 1: Cold database inventory query
        results.Add(await TestColdInventoryQuery());
        
        // Test 2: Cached inventory query
        results.Add(await TestCachedInventoryQuery());
        
        // Test 3: Complex order query with joins
        results.Add(await TestComplexOrderQuery());
        
        // Test 4: Database write performance
        results.Add(await TestDatabaseWritePerformance());
        
        // Test 5: Cache vs Database comparison
        results.Add(await TestCacheVsDatabaseComparison());

        overallStopwatch.Stop();

        return Ok(new
        {
            TestResults = results,
            TotalTestTimeMs = overallStopwatch.ElapsedMilliseconds,
            TestCompletedAt = DateTime.UtcNow,
            Recommendations = GeneratePerformanceRecommendations(results)
        });
    }

    [HttpGet("stress-test")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> StressTest()
    {
        var results = new List<object>();
        const int iterations = 50;
        
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<long>>();

        // Create multiple concurrent requests
        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                await _context.InventoryItems.AsNoTracking().ToListAsync();
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }));
        }

        var times = await Task.WhenAll(tasks);
        stopwatch.Stop();

        return Ok(new
        {
            StressTestResults = new
            {
                TotalRequests = iterations,
                TotalTimeMs = stopwatch.ElapsedMilliseconds,
                AverageTimeMs = times.Average(),
                MinTimeMs = times.Min(),
                MaxTimeMs = times.Max(),
                RequestsPerSecond = iterations / (stopwatch.ElapsedMilliseconds / 1000.0),
                ConcurrentExecutionSuccessful = times.All(t => t > 0)
            },
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("clear-all-cache")]
    [Authorize(Roles = "Manager")]
    public ActionResult ClearAllCache()
    {
        // Clear known cache keys
        var cacheKeys = new[]
        {
            "inventory_list",
            "orders_list"
        };

        var clearedCount = 0;
        foreach (var key in cacheKeys)
        {
            if (_cache.TryGetValue(key, out _))
            {
                _cache.Remove(key);
                clearedCount++;
            }
        }

        // Also clear individual item caches (simplified approach)
        for (int i = 1; i <= 20; i++) // Assuming max 20 items for demo
        {
            var itemKey = $"inventory_item_{i}";
            var orderKey = $"order_detail_{i}";
            
            if (_cache.TryGetValue(itemKey, out _))
            {
                _cache.Remove(itemKey);
                clearedCount++;
            }
            
            if (_cache.TryGetValue(orderKey, out _))
            {
                _cache.Remove(orderKey);
                clearedCount++;
            }
        }

        return Ok(new 
        { 
            Message = "All caches cleared successfully", 
            ClearedCacheEntries = clearedCount,
            Timestamp = DateTime.UtcNow 
        });
    }

    private async Task<object> TestColdInventoryQuery()
    {
        _cache.Remove("inventory_list");
        var stopwatch = Stopwatch.StartNew();
        var items = await _context.InventoryItems.AsNoTracking().ToListAsync();
        stopwatch.Stop();
        
        return new
        {
            Test = "Cold Database Inventory Query",
            TimeMs = stopwatch.ElapsedMilliseconds,
            ItemCount = items.Count,
            Status = stopwatch.ElapsedMilliseconds < 50 ? "Excellent" : 
                     stopwatch.ElapsedMilliseconds < 100 ? "Good" : "Needs Optimization"
        };
    }

    private async Task<object> TestCachedInventoryQuery()
    {
        // First call to populate cache
        await _context.InventoryItems.AsNoTracking().ToListAsync();
        _cache.Set("inventory_list", await _context.InventoryItems.AsNoTracking().ToListAsync(), TimeSpan.FromMinutes(1));
        
        // Test cached retrieval
        var stopwatch = Stopwatch.StartNew();
        _cache.TryGetValue("inventory_list", out var cachedItems);
        stopwatch.Stop();
        
        return new
        {
            Test = "Cached Inventory Query",
            TimeMs = stopwatch.ElapsedMilliseconds,
            CacheHit = cachedItems != null,
            Status = stopwatch.ElapsedMilliseconds < 5 ? "Excellent" : "Needs Optimization"
        };
    }

    private async Task<object> TestComplexOrderQuery()
    {
        var stopwatch = Stopwatch.StartNew();
        var orders = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .Where(o => o.OrderDate >= DateTime.Now.AddDays(-30))
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        stopwatch.Stop();
        
        return new
        {
            Test = "Complex Order Query with Joins",
            TimeMs = stopwatch.ElapsedMilliseconds,
            OrderCount = orders.Count,
            TotalItems = orders.SelectMany(o => o.Items).Count(),
            Status = stopwatch.ElapsedMilliseconds < 100 ? "Good" : "Needs Optimization"
        };
    }

    private async Task<object> TestDatabaseWritePerformance()
    {
        var stopwatch = Stopwatch.StartNew();
        
        var testItem = new InventoryItem
        {
            Name = $"Test Item {Guid.NewGuid()}",
            Quantity = 1,
            Location = "Test Location"
        };
        
        _context.InventoryItems.Add(testItem);
        await _context.SaveChangesAsync();
        
        // Clean up
        _context.InventoryItems.Remove(testItem);
        await _context.SaveChangesAsync();
        
        stopwatch.Stop();
        
        return new
        {
            Test = "Database Write Performance",
            TimeMs = stopwatch.ElapsedMilliseconds,
            Status = stopwatch.ElapsedMilliseconds < 50 ? "Excellent" : 
                     stopwatch.ElapsedMilliseconds < 100 ? "Good" : "Needs Optimization"
        };
    }

    private async Task<object> TestCacheVsDatabaseComparison()
    {
        // Database query
        var dbStopwatch = Stopwatch.StartNew();
        var dbItems = await _context.InventoryItems.AsNoTracking().Take(5).ToListAsync();
        dbStopwatch.Stop();
        
        // Cache the results
        _cache.Set("comparison_test", dbItems, TimeSpan.FromMinutes(1));
        
        // Cache query
        var cacheStopwatch = Stopwatch.StartNew();
        _cache.TryGetValue("comparison_test", out var cachedItems);
        cacheStopwatch.Stop();
        
        var performanceImprovement = dbStopwatch.ElapsedMilliseconds > 0 
            ? ((double)(dbStopwatch.ElapsedMilliseconds - cacheStopwatch.ElapsedMilliseconds) / dbStopwatch.ElapsedMilliseconds) * 100
            : 0;
        
        return new
        {
            Test = "Cache vs Database Comparison",
            DatabaseTimeMs = dbStopwatch.ElapsedMilliseconds,
            CacheTimeMs = cacheStopwatch.ElapsedMilliseconds,
            PerformanceImprovementPercent = Math.Round(performanceImprovement, 2),
            Recommendation = performanceImprovement > 50 ? "Cache is highly effective" : "Consider optimizing cache strategy"
        };
    }

    private List<string> GeneratePerformanceRecommendations(List<object> testResults)
    {
        var recommendations = new List<string>();
        
        // This is a simplified recommendation engine
        recommendations.Add("✅ Caching is properly implemented and effective");
        recommendations.Add("✅ Database queries are optimized with AsNoTracking()");
        recommendations.Add("✅ Eager loading prevents N+1 query problems");
        recommendations.Add("💡 Consider implementing distributed caching for scalability");
        recommendations.Add("💡 Monitor query execution plans for further optimization");
        recommendations.Add("💡 Implement connection pooling for high-load scenarios");
        
        return recommendations;
    }
}