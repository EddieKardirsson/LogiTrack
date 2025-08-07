using System.Text;
using System.Text.Json.Serialization;
using LogiTrack.Models;
using LogiTrack.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<LogiTrackContext>();
        
        // Add Identity services
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Configure password requirements
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            
            // Configure user requirements
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<LogiTrackContext>()
        .AddDefaultTokenProviders();

        // Add JWT Authentication
        var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourVerySecretKeyThatIsAtLeast32CharactersLong";
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LogiTrack";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LogiTrack";

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
            });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        
        builder.Services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
                { 
                    Title = "LogiTrack API", 
                    Version = "v1",
                    Description = "A logistics tracking API with JWT authentication"
                });

                // Add JWT Bearer authentication to Swagger
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Enter your JWT token without the 'Bearer ' prefix"
                });
        
                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

        
        // Add Exception Handling Middleware
        builder.Services.AddExceptionHandler(options =>
        {
            options.ExceptionHandlingPath = "/error";
            options.AllowStatusCode404Response = true;
        });

        var app = builder.Build();
        
        // Seed or Clear the database (comment/uncomment as needed)
        SeedDatabase(app);
        //ClearDatabase(app);

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseHttpsRedirection();
        
        // Add authentication and authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.UseExceptionHandler("/error");
        
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

        app.MapControllers();
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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        
        // Ensure database is created
        context.Database.EnsureCreated();
        
        // Seed roles
        SeedRolesAsync(roleManager).Wait();
        
        // Seed users
        SeedUsersAsync(userManager).Wait();
        
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
                Items = new List<OrderItem> 
                { 
                    new OrderItem(1, 1),  // 1 Crowbar
                    new OrderItem(2, 2),  // 2 Pallet Jacks
                    new OrderItem(10, 1)  // 1 Warehouse Management Software
                }
            },
            new Order 
            { 
                CustomerName = "Luke Skywalker", 
                OrderDate = DateTime.Now.AddDays(-2),
                Items = new List<OrderItem> 
                { 
                    new OrderItem(4, 3),  // 3 Safety Helmets
                    new OrderItem(3, 1),  // 1 Forklift
                    new OrderItem(7, 1)   // 1 Conveyor Belt
                }
            },
            new Order 
            { 
                CustomerName = "Leia Organa", 
                OrderDate = DateTime.Now.AddDays(-1),
                Items = new List<OrderItem> 
                { 
                    new OrderItem(5, 2),  // 2 Loading Docks
                }
            },
            new Order 
            { 
                CustomerName = "Han Solo", 
                OrderDate = DateTime.Now,
                Items = new List<OrderItem> 
                { 
                    new OrderItem(6, 1),  // 1 Hand Truck
                    new OrderItem(8, 3),  // 3 Pallet Racks
                    new OrderItem(9, 1)   // 1 Dock Leveler
                }
            }
        };

        context.Orders.AddRange(orders);
        context.SaveChanges();

        Console.WriteLine("Database seeded successfully!");
    }
    
    // Call this method to clear the database
    // This is useful for testing or resetting the database state.
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Manager", "Employee", "Customer" };
        
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
    
    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        // Create admin user
        if (await userManager.FindByEmailAsync("admin@logitrack.com") == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = "admin@logitrack.com",
                Email = "admin@logitrack.com",
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Manager");
            }
        }
        
        // Create regular user
        if (await userManager.FindByEmailAsync("user@logitrack.com") == null)
        {
            var regularUser = new ApplicationUser
            {
                UserName = "user@logitrack.com",
                Email = "user@logitrack.com",
                FirstName = "Employee",
                LastName = "User",
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(regularUser, "User123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(regularUser, "Employee");
            }
        }
    }

    // Your existing methods...
    private static void ClearDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogiTrackContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        
        Console.WriteLine("Starting database cleanup...");
        
        // Delete business data first (in correct order for foreign key constraints)
        Console.WriteLine("Clearing OrderItems...");
        context.OrderItems.RemoveRange(context.OrderItems);
        
        Console.WriteLine("Clearing Orders...");
        context.Orders.RemoveRange(context.Orders);
        
        Console.WriteLine("Clearing InventoryItems...");
        context.InventoryItems.RemoveRange(context.InventoryItems);
        
        context.SaveChanges();
        
        // Clear Identity data
        Console.WriteLine("Clearing Identity data...");
        
        // Delete user roles relationships first
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserRoles");
        
        // Delete user claims, logins, and tokens
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserClaims");
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserLogins");
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUserTokens");
        
        // Delete role claims
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetRoleClaims");
        
        // Delete users
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetUsers");
        
        // Delete roles
        context.Database.ExecuteSqlRaw("DELETE FROM AspNetRoles");
        
        // Reset the identity/sequence counters for all tables
        Console.WriteLine("Resetting sequence counters...");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='InventoryItems'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='Orders'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='OrderItems'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetUsers'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetRoles'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetUserClaims'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetUserLogins'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetUserTokens'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetUserRoles'");
        context.Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='AspNetRoleClaims'");
        
        Console.WriteLine("Database cleared successfully!");
        
        // Optionally re-seed the roles and default users after clearing
        Console.WriteLine("Re-seeding roles and default users...");
        SeedRolesAsync(roleManager).Wait();
        SeedUsersAsync(userManager).Wait();
        
        Console.WriteLine("Database reset complete!");
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