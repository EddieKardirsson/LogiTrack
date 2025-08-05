using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

public class OrderItem
{
    [Key]
    public int OrderItemId { get; set; }
    
    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; } // Make this nullable
    
    public int QuantityOrdered { get; set; }
    
    public OrderItem() { }
    
    public OrderItem(int inventoryItemId, int quantityOrdered)
    {
        InventoryItemId = inventoryItemId;
        QuantityOrdered = quantityOrdered;
    }
}