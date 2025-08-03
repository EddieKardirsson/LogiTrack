using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogiTrack.Models;

public class InventoryItem
{
    [Key]
    public int ItemId { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    
    [Required]
    public string Location { get; set; } = string.Empty;

    // Foreign key for the one-to-many relationship
    public int? OrderId { get; set; }
    
    // Navigation property
    public Order? Order { get; set; }

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