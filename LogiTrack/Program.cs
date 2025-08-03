using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<LogiTrackContext>();
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Seed the database (comment/uncomment as needed)
        SeedDatabase(app);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        
        app.MapGet("/", () => "Hello World!");
        
        app.MapGet("/items", async (LogiTrackContext context) =>
        {
            var items = await context.InventoryItems.ToListAsync();
            return items.Select(item => new 
            {
                item.ItemId,
                item.Name,
                item.Quantity,
                item.Location
            });
        });

        app.MapGet("/orders", async (LogiTrackContext context) =>
        {
            var orders = await context.Orders.Include(o => o.Items).ToListAsync();
            return orders.Select(order => new
            {
                order.OrderId,
                order.CustomerName,
                order.OrderDate,
                ItemCount = order.Items.Count,
                Summary = order.GetOrderSummary()
            });
        });

        app.Run();
    }

    // Seed the database with initial data if it is empty
    // This method can be called during development to populate the database with sample data.
    // In production, you might want to remove or modify this to avoid overwriting existing data
    // or to use a more sophisticated seeding strategy.
    private static void SeedDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
        
        // Check if data already exists
        if (context.InventoryItems.Any() || context.Orders.Any())
        {
            return; // Database already seeded
        }

        // Seed Inventory Items
        var inventoryItems = new List<InventoryItem>
        {
            new InventoryItem { Name = "Crowbar", Quantity = 100, Location = "Warehouse 1" },
            new InventoryItem { Name = "Pallet Jack", Quantity = 12, Location = "Warehouse A" },
            new InventoryItem { Name = "Forklift", Quantity = 5, Location = "Warehouse 1" },
            new InventoryItem { Name = "Safety Helmet", Quantity = 50, Location = "Warehouse B" },
            new InventoryItem { Name = "Loading Dock", Quantity = 7, Location = "Warehouse 1" },
            new InventoryItem { Name = "Hand Truck", Quantity = 20, Location = "Warehouse C" },
            new InventoryItem { Name = "Conveyor Belt", Quantity = 3, Location = "Warehouse D" },
            new InventoryItem { Name = "Pallet Racks", Quantity = 15, Location = "Warehouse 1" },
            new InventoryItem { Name = "Dock Leveler", Quantity = 4, Location = "Warehouse 2" },
            new InventoryItem { Name = "Warehouse Management Software", Quantity = 4, Location = "IT Department" }
        };

        context.InventoryItems.AddRange(inventoryItems);
        context.SaveChanges();

        // Seed Orders
        var orders = new List<Order>
        {
            new Order 
            { 
                CustomerName = "Palpatine", 
                OrderDate = DateTime.Now.AddDays(-5),
                Items = new List<InventoryItem> 
                { 
                    inventoryItems[0], // Crowbar
                    inventoryItems[1],  // Pallet Jack
                    inventoryItems[1],  // Pallet Jack
                    inventoryItems[9]  // Warehouse Management Software
                }
            },
            new Order 
            { 
                CustomerName = "Luke Skywalker", 
                OrderDate = DateTime.Now.AddDays(-2),
                Items = new List<InventoryItem> 
                { 
                    inventoryItems[3], // Safety Helmet
                    inventoryItems[2],  // Forklift
                    inventoryItems[6]  // Conveyor Belt
                }
            },
            new Order 
            { 
                CustomerName = "Leia Organa", 
                OrderDate = DateTime.Now.AddDays(-1),
                Items = new List<InventoryItem> 
                { 
                    inventoryItems[4], // Loading Dock
                    inventoryItems[4]  // Loading Dock
                }
            },
            new Order 
            { 
                CustomerName = "Han Solo", 
                OrderDate = DateTime.Now,
                Items = new List<InventoryItem> 
                { 
                    inventoryItems[5], // Hand Truck
                    inventoryItems[7],  // Pallet Racks
                    inventoryItems[8]  // Dock Leveler
                }
            }
        };

        context.Orders.AddRange(orders);
        context.SaveChanges();

        Console.WriteLine("Database seeded successfully!");
    }
}