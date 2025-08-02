namespace LogiTrack.Models;

public class Order
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public List<InventoryItem> Items { get; set; } = new List<InventoryItem>();
    
    public Order() { }
    
    public Order(int orderId, string customerName, DateTime orderDate, List<InventoryItem> items)
    {
        OrderId = orderId;
        CustomerName = customerName;
        OrderDate = orderDate;
        Items = items;
    }
    
    public void AddItem(InventoryItem item) => Items.Add(item);
    
    public void RemoveItem(int itemId) => Items.Remove(Items.Find(item => item.ItemId == itemId));
    
    public string GetOrderSummary() => $"Order #{OrderId} for {CustomerName} on {OrderDate.ToShortDateString()} with {Items.Count} items.";
}