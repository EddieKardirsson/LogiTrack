using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models;

public class Order
{
    [Key]
    public int OrderId { get; set; }
    
    [Required]
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    
    // Collection of order items (each with quantity)
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    
    public Order() { }
    
    public Order(int orderId, string customerName, DateTime orderDate)
    {
        OrderId = orderId;
        CustomerName = customerName;
        OrderDate = orderDate;
    }
    
    public bool AddItem(int inventoryItemId, int quantity) 
    {
        if (quantity <= 0) return false;
        
        // Check if item already exists in order
        var existingOrderItem = Items.FirstOrDefault(oi => oi.InventoryItemId == inventoryItemId);
        if (existingOrderItem != null)
        {
            existingOrderItem.QuantityOrdered += quantity;
        }
        else
        {
            Items.Add(new OrderItem(inventoryItemId, quantity));
        }
        
        return true;
    }
    
    public void RemoveItem(int inventoryItemId) 
    {
        var orderItem = Items.FirstOrDefault(oi => oi.InventoryItemId == inventoryItemId);
        if (orderItem != null)
        {
            Items.Remove(orderItem);
        }
    }
    
    public string GetOrderSummary() => $"Order #{OrderId} for {CustomerName} on {OrderDate.ToShortDateString()} with {Items.Sum(i => i.QuantityOrdered)} total items.";
}