using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class OrderController : ControllerBase
{
    private readonly LogiTrackContext context;
    
    public OrderController(LogiTrackContext context) => this.context = context;
    
    // GET: api/orders
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Order>>> GetAllOrders()
    {
        var orders = await context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();
        
        return Ok(orders);
    }
    
    // GET: api/orders/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetOrderById(int id)
    {
        var order = await context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (order == null) return NotFound();
        
        return Ok(order);
    }
    
    // POST: api/orders
    [HttpPost]
    public async Task<ActionResult<Order>> CreateOrder(Order newOrder)
    {
        if(newOrder == null || newOrder.Items == null || string.IsNullOrWhiteSpace(newOrder.CustomerName)) 
            return BadRequest("Invalid order data.");
        
        context.Orders.Add(newOrder);
        await context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetOrderById), new { id = newOrder.OrderId }, newOrder);
    }
    
    // PUT: api/orders/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<Order>> UpdateOrder(int id, Order updatedOrder)
    {
        if (updatedOrder == null || updatedOrder.Items == null || string.IsNullOrWhiteSpace(updatedOrder.CustomerName))
            return BadRequest("Invalid order data.");
        
        var existingOrder = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        
        if (existingOrder == null) return NotFound();
        
        existingOrder.CustomerName = updatedOrder.CustomerName;
        existingOrder.OrderDate = updatedOrder.OrderDate;
        existingOrder.Items = updatedOrder.Items; // This will replace the items
        
        context.Orders.Update(existingOrder);
        await context.SaveChangesAsync();
        
        return Ok(existingOrder);
    }
    
    // DELETE: api/orders/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteOrder(int id)
    {
        var order = await context.Orders.FindAsync(id);
        if (order == null) return NotFound();
        
        context.Orders.Remove(order);
        await context.SaveChangesAsync();
        
        return NoContent();
    }
}