using Microsoft.AspNetCore.Mvc;
using LogiTrack.Data;
using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly LogiTrackContext context;
    
    public InventoryController(LogiTrackContext context) => this.context = context;
    
    // GET: api/inventory
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetAllItems()
    {
        var items = await context.InventoryItems.AsNoTracking().ToListAsync();
        return Ok(items);
    }
    
    // GET: api/inventory/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<InventoryItem>> GetItemById(int id)
    {
        var item = await context.InventoryItems.FindAsync(id);
        if(item == null) return NotFound();
        return Ok(item);
    }
    
    // POST: api/inventory
    [HttpPost]
    public async Task<ActionResult<InventoryItem>> CreateItem(InventoryItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name) || item.Quantity < 0 || string.IsNullOrWhiteSpace(item.Location))
        {
            return BadRequest("Invalid inventory item data.");
        }
        
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetItemById), new { id = item.ItemId }, item);
    }

    // Method to update an existing inventory item with more quantity of the particular item
    // PUT: api/inventory/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult<InventoryItem>> AddItemQuantity(int id, [FromBody] int addedQuantity)
    {
        var item = await context.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();

        if (addedQuantity <= 0)
        {
            return BadRequest("Quantity cannot be zero or negative.");
        }

        item.Quantity += addedQuantity;
        context.InventoryItems.Update(item);
        await context.SaveChangesAsync();

        return Ok(item);
    }

    // DELETE: api/inventory/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteItem(int id)
    {
        var item = await context.InventoryItems.FindAsync(id);
        if (item == null) return NotFound();
        context.InventoryItems.Remove(item);
        await context.SaveChangesAsync();
        return NoContent();
    }
}