using LogiTrack.Models;

namespace LogiTrack;

public class Program
{
    public static void Main(string[] args)
    {
        List<InventoryItem> inventory = new List<InventoryItem>();
        inventory.Add(new InventoryItem { ItemId = 1, Name = "Crowbar", Quantity = 100, Location = "Warehouse 1" });
        inventory.Add(new InventoryItem { ItemId = 2, Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" });
        
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
            return inventory.Select(item => new 
            {
                item.ItemId,
                item.Name,
                item.Quantity,
                item.Location
            });
        });

        app.Run();
        
    }
}