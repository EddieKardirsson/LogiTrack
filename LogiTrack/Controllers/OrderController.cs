using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]s")]
[Authorize] // Require authentication for all endpoints
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext context;
    
    public OrderController(LogiTrackContext context) => this.context = context;
    
    // GET: api/orders - Anyone authenticated can view
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
    {
        var orders = await context.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .ToListAsync();
    
        return Ok(orders);
    }

    // GET: api/orders/{id} - Anyone authenticated can view
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrderById(int id)
    {
        var order = await context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (order == null) return NotFound();
        
        return Ok(order);
    }
    
    // POST: api/orders - Managers and Employees can create
    [HttpPost]
    [Authorize(Roles = "Manager,Employee")]
    public async Task<ActionResult<Order>> CreateOrder(Order newOrder)
    {
        if(newOrder == null || string.IsNullOrWhiteSpace(newOrder.CustomerName)) 
            return BadRequest("Invalid order data.");
        
        // Validate that all inventory items exist
        if (newOrder.Items != null && newOrder.Items.Any())
        {
            var inventoryItemIds = newOrder.Items.Select(oi => oi.InventoryItemId).ToList();
            var existingItems = await context.InventoryItems
                .Where(i => inventoryItemIds.Contains(i.ItemId))
                .Select(i => i.ItemId)
                .ToListAsync();
            
            var missingItems = inventoryItemIds.Except(existingItems).ToList();
            if (missingItems.Any())
            {
                return BadRequest($"The following inventory items do not exist: {string.Join(", ", missingItems)}");
            }
        }
        
        // Clear any InventoryItem navigation properties to avoid issues
        foreach (var item in newOrder.Items)
        {
            item.InventoryItem = null!;
        }
        
        context.Orders.Add(newOrder);
        await context.SaveChangesAsync();
        
        // Load the created order with its items and inventory details
        var createdOrder = await context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .FirstOrDefaultAsync(o => o.OrderId == newOrder.OrderId);
        
        return CreatedAtAction(nameof(GetOrderById), new { id = newOrder.OrderId }, createdOrder);
    }

    // PUT: api/orders/{id} - Only Managers can update
    [HttpPut("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<Order>> UpdateOrder(int id, Order updatedOrder)
    {
        if (updatedOrder == null || string.IsNullOrWhiteSpace(updatedOrder.CustomerName))
            return BadRequest("Invalid order data.");
        
        var existingOrder = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (existingOrder == null) return NotFound();
        
        // Validate that all inventory items exist
        if (updatedOrder.Items != null && updatedOrder.Items.Any())
        {
            var inventoryItemIds = updatedOrder.Items.Select(oi => oi.InventoryItemId).ToList();
            var existingItems = await context.InventoryItems
                .Where(i => inventoryItemIds.Contains(i.ItemId))
                .Select(i => i.ItemId)
                .ToListAsync();
            
            var missingItems = inventoryItemIds.Except(existingItems).ToList();
            if (missingItems.Any())
            {
                return BadRequest($"The following inventory items do not exist: {string.Join(", ", missingItems)}");
            }
        }
        
        // Update basic properties
        existingOrder.CustomerName = updatedOrder.CustomerName;
        existingOrder.OrderDate = updatedOrder.OrderDate;
        
        // Remove existing order items
        context.OrderItems.RemoveRange(existingOrder.Items);
        
        // Add new order items
        existingOrder.Items.Clear();
        if (updatedOrder.Items != null)
        {
            foreach (var item in updatedOrder.Items)
            {
                item.InventoryItem = null!; // Clear navigation property
                existingOrder.Items.Add(item);
            }
        }
        
        await context.SaveChangesAsync();
        
        // Load the updated order with its items and inventory details
        var result = await context.Orders
            .Include(o => o.Items)
                .ThenInclude(oi => oi.InventoryItem)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        return Ok(result);
    }

    // DELETE: api/orders/{id} - Only Managers can delete
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult> DeleteOrder(int id)
    {
        var order = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (order == null) return NotFound();
        
        // Remove all order items first
        if (order.Items.Any())
        {
            context.OrderItems.RemoveRange(order.Items);
        }
        
        // Then remove the order
        context.Orders.Remove(order);
        await context.SaveChangesAsync();
        
        return NoContent();
    }
}