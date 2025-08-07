using Microsoft.AspNetCore.Mvc;
using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryController> _logger;
    
    // Cache keys constants
    private const string INVENTORY_LIST_CACHE_KEY = "inventory_list";
    private const string INVENTORY_ITEM_CACHE_KEY = "inventory_item_{0}";
    
    public InventoryController(LogiTrackContext context, IMemoryCache cache, ILogger<InventoryController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    // GET: api/inventory - Anyone authenticated can view (WITH CACHING)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllItems()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Try to get data from cache first
        if (_cache.TryGetValue(INVENTORY_LIST_CACHE_KEY, out List<InventoryItem>? cachedItems))
        {
            stopwatch.Stop();
            _logger.LogInformation("Retrieved {Count} inventory items from cache in {ElapsedMilliseconds}ms", 
                cachedItems!.Count, stopwatch.ElapsedMilliseconds);
            return Ok(cachedItems);
        }

        // If not in cache, fetch from database
        var items = await _context.InventoryItems
            .AsNoTracking() // No tracking needed for read-only operations
            .OrderBy(i => i.Name) // Consistent ordering for better user experience
            .ToListAsync();

        // Cache the result for 30 seconds
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
            Priority = CacheItemPriority.High,
            Size = items.Count // Help with cache size management
        };
        
        _cache.Set(INVENTORY_LIST_CACHE_KEY, items, cacheOptions);
        
        stopwatch.Stop();
        _logger.LogInformation("Retrieved {Count} inventory items from database and cached in {ElapsedMilliseconds}ms", 
            items.Count, stopwatch.ElapsedMilliseconds);
        
        return Ok(items);
    }
    
    // GET: api/inventory/{id} - Anyone authenticated can view (WITH CACHING)
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetItemById(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheKey = string.Format(INVENTORY_ITEM_CACHE_KEY, id);
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out InventoryItem? cachedItem))
        {
            stopwatch.Stop();
            if (cachedItem == null)
            {
                _logger.LogInformation("Item {Id} not found (from cache) in {ElapsedMilliseconds}ms", 
                    id, stopwatch.ElapsedMilliseconds);
                return NotFound();
            }
            
            _logger.LogInformation("Retrieved inventory item {Id} from cache in {ElapsedMilliseconds}ms", 
                id, stopwatch.ElapsedMilliseconds);
            return Ok(cachedItem);
        }

        // If not in cache, fetch from database
        var item = await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.ItemId == id);
        
        // Cache the result (including null results to avoid repeated DB calls)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), // Individual items cached longer
            Priority = CacheItemPriority.Normal
        };
        
        _cache.Set(cacheKey, item, cacheOptions);
        
        stopwatch.Stop();
        if (item == null)
        {
            _logger.LogInformation("Item {Id} not found (from database) in {ElapsedMilliseconds}ms", 
                id, stopwatch.ElapsedMilliseconds);
            return NotFound();
        }
        
        _logger.LogInformation("Retrieved inventory item {Id} from database and cached in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);
        
        return Ok(item);
    }
    
    // POST: api/inventory - Only Managers can create
    [HttpPost]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> CreateItem(InventoryItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Quantity < 0 || string.IsNullOrWhiteSpace(item.Location))
        {
            return BadRequest("Invalid inventory item data.");
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();
        
        // Invalidate cache when data changes
        InvalidateInventoryCache();
        
        stopwatch.Stop();
        _logger.LogInformation("Created new inventory item {ItemId} in {ElapsedMilliseconds}ms", 
            item.ItemId, stopwatch.ElapsedMilliseconds);

        return CreatedAtAction(nameof(GetItemById), new { id = item.ItemId }, item);
    }

    // PUT: api/inventory/{id} - Only Managers can update
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<InventoryItem>> AddItemQuantity(int id, [FromBody] int addedQuantity)
    {
        if (addedQuantity <= 0)
        {
            return BadRequest("Quantity cannot be zero or negative.");
        }

        var stopwatch = Stopwatch.StartNew();
        
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null) 
        {
            stopwatch.Stop();
            return NotFound();
        }

        item.Quantity += addedQuantity;
        _context.InventoryItems.Update(item);
        await _context.SaveChangesAsync();
        
        // Invalidate cache when data changes
        InvalidateInventoryCache();
        InvalidateSpecificItemCache(id);
        
        stopwatch.Stop();
        _logger.LogInformation("Updated inventory item {ItemId} quantity in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);

        return Ok(item);
    }

    // DELETE: api/inventory/{id} - Only Managers can delete
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> DeleteItem(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            stopwatch.Stop();
            return NotFound();
        }
        
        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();
        
        // Invalidate cache when data changes
        InvalidateInventoryCache();
        InvalidateSpecificItemCache(id);
        
        stopwatch.Stop();
        _logger.LogInformation("Deleted inventory item {ItemId} in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);
        
        return NoContent();
    }
    
    // Helper method to invalidate inventory list cache
    private void InvalidateInventoryCache()
    {
        _cache.Remove(INVENTORY_LIST_CACHE_KEY);
        _logger.LogDebug("Invalidated inventory list cache");
    }
    
    // Helper method to invalidate specific item cache
    private void InvalidateSpecificItemCache(int id)
    {
        var cacheKey = string.Format(INVENTORY_ITEM_CACHE_KEY, id);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for inventory item {ItemId}", id);
    }
}