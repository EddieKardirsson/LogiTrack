using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogiTrack.Models;

public class Order
{
    [Key]
    public int OrderId { get; set; }
    
    [Required]
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    
    // Navigation property for the one-to-many relationship
    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
    
    public Order() { }
    
    public Order(int orderId, string customerName, DateTime orderDate)
    {
        OrderId = orderId;
        CustomerName = customerName;
        OrderDate = orderDate;
    }
    
    public bool AddItem(InventoryItem item) 
    {
        // Input validation
        if (item == null)
            return false;
        
        Items.Add(item);
        item.OrderId = OrderId;
        item.Order = this;

        return true;
    }
    
    public void RemoveItem(int itemId) 
    {
        var item = Items.FirstOrDefault(i => i.ItemId == itemId);
        if (item != null)
        {
            Items.Remove(item);
            item.OrderId = null;
            item.Order = null;
        }
    }
    
    public string GetOrderSummary() => $"Order #{OrderId} for {CustomerName} on {OrderDate.ToShortDateString()} with {Items.Count} items.";
}