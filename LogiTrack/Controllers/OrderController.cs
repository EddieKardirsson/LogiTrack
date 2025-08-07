using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]s")]
[Authorize] // Require authentication for all endpoints
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrderController> _logger;
    
    // Cache keys
    private const string ORDERS_LIST_CACHE_KEY = "orders_list";
    private const string ORDER_DETAIL_CACHE_KEY = "order_detail_{0}";
    
    public OrderController(LogiTrackContext context, IMemoryCache cache, ILogger<OrderController> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }
    
    // GET: api/orders - Anyone authenticated can view (OPTIMIZED WITH CACHING)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAllOrders()
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Try cache first
        if (_cache.TryGetValue(ORDERS_LIST_CACHE_KEY, out List<object>? cachedOrders))
        {
            stopwatch.Stop();
            _logger.LogInformation("Retrieved {Count} orders from cache in {ElapsedMilliseconds}ms", 
                cachedOrders!.Count, stopwatch.ElapsedMilliseconds);
            return Ok(cachedOrders);
        }

        // Optimized query - use projection to avoid loading unnecessary data
        // and eager load related data to prevent N+1 queries
        var orders = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking() // No tracking for read-only
            .OrderByDescending(o => o.OrderDate) // Most recent first
            .Select(o => new
            {
                o.OrderId,
                o.CustomerName,
                o.OrderDate,
                ItemCount = o.Items.Count,
                TotalValue = o.Items.Sum(oi => oi.QuantityOrdered * (oi.InventoryItem != null ? oi.InventoryItem.Name.Length : 0)), // Simple calculation
                Items = o.Items.Select(oi => new
                {
                    oi.OrderItemId,
                    oi.QuantityOrdered,
                    InventoryItem = oi.InventoryItem != null ? new
                    {
                        oi.InventoryItem.ItemId,
                        oi.InventoryItem.Name,
                        oi.InventoryItem.Location
                    } : null
                }).ToList()
            })
            .ToListAsync();
        
        // Cache for 60 seconds
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
            Priority = CacheItemPriority.High,
            Size = orders.Count
        };
        
        _cache.Set(ORDERS_LIST_CACHE_KEY, orders, cacheOptions);
        
        stopwatch.Stop();
        _logger.LogInformation("Retrieved {Count} orders from database and cached in {ElapsedMilliseconds}ms", 
            orders.Count, stopwatch.ElapsedMilliseconds);

        return Ok(orders);
    }

    // GET: api/orders/{id} - Anyone authenticated can view (OPTIMIZED WITH CACHING)
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrderById(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        var cacheKey = string.Format(ORDER_DETAIL_CACHE_KEY, id);
        
        // Try cache first
        if (_cache.TryGetValue(cacheKey, out Order? cachedOrder))
        {
            stopwatch.Stop();
            if (cachedOrder == null)
            {
                _logger.LogInformation("Order {Id} not found (from cache) in {ElapsedMilliseconds}ms", 
                    id, stopwatch.ElapsedMilliseconds);
                return NotFound();
            }
            
            _logger.LogInformation("Retrieved order {Id} from cache in {ElapsedMilliseconds}ms", 
                id, stopwatch.ElapsedMilliseconds);
            return Ok(cachedOrder);
        }

        // Single optimized query with all needed data
        var order = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        // Cache individual order for longer (2 minutes)
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2),
            Priority = CacheItemPriority.Normal
        };
        
        _cache.Set(cacheKey, order, cacheOptions);
        
        stopwatch.Stop();
        if (order == null)
        {
            _logger.LogInformation("Order {Id} not found (from database) in {ElapsedMilliseconds}ms", 
                id, stopwatch.ElapsedMilliseconds);
            return NotFound();
        }
        
        _logger.LogInformation("Retrieved order {Id} from database and cached in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);
        
        return Ok(order);
    }
    
    // POST: api/orders - Managers and Employees can create (OPTIMIZED)
    [HttpPost]
    [Authorize(Roles = "Manager,Employee")]
    public async Task<ActionResult<Order>> CreateOrder(Order newOrder)
    {
        if(newOrder == null || string.IsNullOrWhiteSpace(newOrder.CustomerName)) 
            return BadRequest("Invalid order data.");
        
        var stopwatch = Stopwatch.StartNew();
        
        // Optimized validation - single query to check all items at once
        if (newOrder.Items != null && newOrder.Items.Any())
        {
            var inventoryItemIds = newOrder.Items.Select(oi => oi.InventoryItemId).Distinct().ToList();
            var existingItemIds = await _context.InventoryItems
                .Where(i => inventoryItemIds.Contains(i.ItemId))
                .Select(i => i.ItemId)
                .ToListAsync();
            
            var missingItems = inventoryItemIds.Except(existingItemIds).ToList();
            if (missingItems.Any())
            {
                stopwatch.Stop();
                return BadRequest($"The following inventory items do not exist: {string.Join(", ", missingItems)}");
            }
        }
        
        // Clear navigation properties to avoid issues
        if (newOrder.Items != null)
        {
            foreach (var item in newOrder.Items)
            {
                item.InventoryItem = null!;
            }
        }
        
        _context.Orders.Add(newOrder);
        await _context.SaveChangesAsync();
        
        // Load the created order with optimized query
        var createdOrder = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == newOrder.OrderId);
        
        // Invalidate caches
        InvalidateOrderCaches();
        
        stopwatch.Stop();
        _logger.LogInformation("Created order {OrderId} in {ElapsedMilliseconds}ms", 
            newOrder.OrderId, stopwatch.ElapsedMilliseconds);
        
        return CreatedAtAction(nameof(GetOrderById), new { id = newOrder.OrderId }, createdOrder);
    }

    // PUT: api/orders/{id} - Only Managers can update (OPTIMIZED)
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Order>> UpdateOrder(int id, Order updatedOrder)
    {
        if (updatedOrder == null || string.IsNullOrWhiteSpace(updatedOrder.CustomerName))
            return BadRequest("Invalid order data.");
        
        var stopwatch = Stopwatch.StartNew();
        
        // Single query to get existing order
        var existingOrder = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (existingOrder == null)
        {
            stopwatch.Stop();
            return NotFound();
        }
        
        // Validate inventory items if provided
        if (updatedOrder.Items != null && updatedOrder.Items.Any())
        {
            var inventoryItemIds = updatedOrder.Items.Select(oi => oi.InventoryItemId).Distinct().ToList();
            var existingItemIds = await _context.InventoryItems
                .Where(i => inventoryItemIds.Contains(i.ItemId))
                .Select(i => i.ItemId)
                .ToListAsync();
            
            var missingItems = inventoryItemIds.Except(existingItemIds).ToList();
            if (missingItems.Any())
            {
                stopwatch.Stop();
                return BadRequest($"The following inventory items do not exist: {string.Join(", ", missingItems)}");
            }
        }
        
        // Update properties
        existingOrder.CustomerName = updatedOrder.CustomerName;
        existingOrder.OrderDate = updatedOrder.OrderDate;
        
        // Efficient update of order items
        _context.OrderItems.RemoveRange(existingOrder.Items);
        existingOrder.Items.Clear();
        
        if (updatedOrder.Items != null)
        {
            foreach (var item in updatedOrder.Items)
            {
                item.InventoryItem = null!;
                existingOrder.Items.Add(item);
            }
        }
        
        await _context.SaveChangesAsync();
        
        // Load updated order with single query
        var result = await _context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        // Invalidate caches
        InvalidateOrderCaches();
        InvalidateSpecificOrderCache(id);
        
        stopwatch.Stop();
        _logger.LogInformation("Updated order {OrderId} in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);
        
        return Ok(result);
    }

    // DELETE: api/orders/{id} - Only Managers can delete (OPTIMIZED)
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> DeleteOrder(int id)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Single query with items
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (order == null)
        {
            stopwatch.Stop();
            return NotFound();
        }
        
        // Cascade delete will handle order items automatically
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        
        // Invalidate caches
        InvalidateOrderCaches();
        InvalidateSpecificOrderCache(id);
        
        stopwatch.Stop();
        _logger.LogInformation("Deleted order {OrderId} in {ElapsedMilliseconds}ms", 
            id, stopwatch.ElapsedMilliseconds);
        
        return NoContent();
    }
    
    // Helper methods for cache management
    private void InvalidateOrderCaches()
    {
        _cache.Remove(ORDERS_LIST_CACHE_KEY);
        _logger.LogDebug("Invalidated orders list cache");
    }
    
    private void InvalidateSpecificOrderCache(int id)
    {
        var cacheKey = string.Format(ORDER_DETAIL_CACHE_KEY, id);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for order {OrderId}", id);
    }
}