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
            var orderSummaries = await context.Orders
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerName,
                    OrderDate = o.OrderDate.ToString("MM/dd/yyyy"),
                    ItemCount = o.Items.Count(),
                    Summary = $"Order #{o.OrderId} for {o.CustomerName} on {o.OrderDate.ToString("MM/dd/yyyy")} with {o.Items.Count()} items."
                })
                .ToListAsync();

            return orderSummaries;
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
    
    // Call this method to clear the database
    // This is useful for testing or resetting the database state.
    private static void ClearDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
        
        context.InventoryItems.RemoveRange(context.InventoryItems);
        context.Orders.RemoveRange(context.Orders);
        
        // Reset the index counters to start from 1 again
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='InventoryItems'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='Orders'");
        
        context.SaveChanges();
        
        Console.WriteLine("Database cleared successfully!");
    }

    // This method prints order summaries in batches to avoid loading too many records into memory at once. Great for large datasets.
    // It processes orders in batches of a specified size, defaulting to 100.
    // You can adjust the batch size as needed.
    // This is useful for scenarios where you want to display or process large numbers of orders without overwhelming the system memory.
    // It uses asynchronous operations to ensure that the application remains responsive while processing the data.
    public static async Task PrintOrderSummariesInBatchesAsync(LogiTrackContext context, int batchSize = 100)
    {
        var totalOrders = await context.Orders.CountAsync();
        var processedOrders = 0;
        
        while (processedOrders < totalOrders)
        {
            var batch = await context.Orders
                .Skip(processedOrders)
                .Take(batchSize)
                .Select(o => new { o.OrderId, o.CustomerName, o.OrderDate, ItemCount = o.Items.Count() })
                .ToListAsync();
        
            foreach (var order in batch)
            {
                Console.WriteLine($"Order #{order.OrderId} for {order.CustomerName} on {order.OrderDate.ToShortDateString()} with {order.ItemCount} items.");
            }
        
            processedOrders += batch.Count;
        }
    }
    
    // This method prints all order summaries in a single operation.
    // It retrieves all orders from the database and displays their summaries.
    // This is useful for scenarios where you want to quickly view all orders without pagination or batching.
    // It uses asynchronous operations to ensure that the application remains responsive while retrieving and displaying the data.
    // Note: This method may not be suitable for very large datasets as it loads all orders into memory at once. Only use it when you are sure the dataset is manageable.
    public static async Task PrintAllOrderSummariesAsync(LogiTrackContext context)
    {
        var orders = await context.Orders
            .Select(o => new { o.OrderId, o.CustomerName, o.OrderDate, ItemCount = o.Items.Count() })
            .ToListAsync();
        
        Console.WriteLine("=== Order Summaries ===");
        foreach (var order in orders)
        {
            Console.WriteLine($"Order #{order.OrderId} for {order.CustomerName} on {order.OrderDate.ToShortDateString()} with {order.ItemCount} items.");
        }
        Console.WriteLine($"Total Orders: {orders.Count}");
    }
}