using Microsoft.AspNetCore.Mvc;
using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
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

    [HttpGet("test-inventory-performance")]
    public async Task<ActionResult> TestInventoryPerformance()
    {
        var results = new List<object>();

        // Test 1: Cold database call (no cache)
        _cache.Remove("inventory_list");
        var stopwatch = Stopwatch.StartNew();
        var items1 = await _context.InventoryItems.AsNoTracking().ToListAsync();
        stopwatch.Stop();
        results.Add(new { Test = "Cold Database Call", TimeMs = stopwatch.ElapsedMilliseconds, ItemCount = items1.Count });

        // Test 2: Warm database call (should hit cache)
        stopwatch.Restart();
        var response = await new HttpClient().GetAsync($"{Request.Scheme}://{Request.Host}/api/inventory");
        stopwatch.Stop();
        results.Add(new { Test = "First API Call (Cache Miss)", TimeMs = stopwatch.ElapsedMilliseconds, Status = response.StatusCode });

        // Test 3: Cached call
        await Task.Delay(100); // Small delay to ensure cache is set
        stopwatch.Restart();
        response = await new HttpClient().GetAsync($"{Request.Scheme}://{Request.Host}/api/inventory");
        stopwatch.Stop();
        results.Add(new { Test = "Second API Call (Cache Hit)", TimeMs = stopwatch.ElapsedMilliseconds, Status = response.StatusCode });

        return Ok(new
        {
            TestResults = results,
            CacheInfo = new
            {
                HasInventoryCache = _cache.TryGetValue("inventory_list", out _),
                Timestamp = DateTime.UtcNow
            }
        });
    }

    [HttpGet("clear-cache")]
    [Authorize(Roles = "Manager")]
    public ActionResult ClearCache()
    {
        // Clear specific cache keys
        var cacheKeys = new[]
        {
            "inventory_list",
            "orders_list"
        };

        foreach (var key in cacheKeys)
        {
            _cache.Remove(key);
        }

        // Can also implement a more sophisticated cache clearing mechanism if tracking all cache keys

        return Ok(new { Message = "Cache cleared successfully", Timestamp = DateTime.UtcNow });
    }
}
