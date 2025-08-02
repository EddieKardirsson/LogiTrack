namespace LogiTrack.Models;

public class InventoryItem
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Location { get; set; } = string.Empty;

    public InventoryItem() { }
    
    public InventoryItem(int itemId, string name, int quantity, string location)
    {
        ItemId = itemId;
        Name = name;
        Quantity = quantity;
        Location = location;
    }
    
    public string GetItemInfo()
    {
        return $"Item: {Name} | Quantity: {Quantity} | Location: {Location}";
    }
    public void DisplayInfo() => Console.WriteLine(GetItemInfo());
}