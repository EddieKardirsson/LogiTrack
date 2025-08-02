using LogiTrack.Models;

namespace LogiTrack;

public class Program
{
    public static void Main(string[] args)
    {
        List<InventoryItem> inventory = new List<InventoryItem>();
        inventory.Add(new InventoryItem { ItemId = 1, Name = "Crowbar", Quantity = 100, Location = "Warehouse 1" });
        inventory.Add(new InventoryItem { ItemId = 2, Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" });
        
        Order testOrder = new Order
        {
            OrderId = 66,
            CustomerName = "Palpatine",
            OrderDate = DateTime.Now,
            Items = new List<InventoryItem>
            {
                new InventoryItem { ItemId = 1, Name = "Crowbar", Quantity = 2, Location = "Warehouse 1" },
                new InventoryItem { ItemId = 2, Name = "Pallet Jack", Quantity = 1, Location = "Warehouse A" }
            }
        };
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();
        
        app.MapGet("/", () => "Hello World!");
        app.MapGet("/items", () =>
        {
            return string.Join("\n", inventory.Select(item => item.GetItemInfo()));
        });

        app.MapGet("/orders", () => testOrder.GetOrderSummary());

        app.Run();
        
    }
}