namespace Moongazing.OrionKey.EntityFrameworkCore.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Round-trips typed ids through a real SQLite provider with the conversions registered model-wide by
/// <see cref="OrionKeyModelBuilderExtensions.UseOrionKeyConversions"/>, with no per-property
/// <c>HasConversion</c> / <c>HasOrionKeyConversion</c> call anywhere in the context.
/// </summary>
public sealed class UseOrionKeyConversionsTests
{
    private sealed class Order
    {
        public OrderId Id { get; set; }
        public CustomerId CustomerId { get; set; }
        public TenantId TenantId { get; set; }
        public string Description { get; set; } = "";
    }

    private sealed class ConventionDbContext(DbContextOptions<ConventionDbContext> options)
        : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().HasKey(o => o.Id);

            // The whole point: a single call wires OrderId, CustomerId, and TenantId. No per-property
            // converter configuration is present in this context.
            modelBuilder.UseOrionKeyConversions();
        }
    }

    private static async Task<(SqliteConnection Connection, DbContextOptions<ConventionDbContext> Options)>
        OpenAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConventionDbContext>()
            .UseSqlite(connection)
            .Options;
        return (connection, options);
    }

    [Fact]
    public async Task Convention_PersistsAndReloads_EveryIdShape_WithoutPerPropertyConfig()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;

        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var tenantId = TenantId.New();

        await using (var ctx = new ConventionDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                TenantId = tenantId,
                Description = "first",
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ConventionDbContext(options))
        {
            var reloaded = await ctx.Orders.SingleAsync();
            Assert.Equal(orderId, reloaded.Id);
            Assert.Equal(customerId, reloaded.CustomerId);
            Assert.Equal(tenantId, reloaded.TenantId);
            Assert.Equal("first", reloaded.Description);
        }
    }

    [Fact]
    public async Task Convention_StoresUnderlyingPrimitive_NotAnOpaqueStruct()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;

        var orderId = OrderId.New();
        var customerId = CustomerId.New();
        var tenantId = TenantId.New();

        await using (var ctx = new ConventionDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Orders.Add(new Order
            {
                Id = orderId,
                CustomerId = customerId,
                TenantId = tenantId,
            });
            await ctx.SaveChangesAsync();
        }

        // Read the raw columns back through ADO.NET: the Guid id is stored as TEXT (SQLite Guid form),
        // the long id as INTEGER, and the string id as its raw string. The point is that the column
        // holds the underlying primitive, proving the converter ran.
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CustomerId, TenantId FROM Orders LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        var storedCustomer = reader.GetInt64(0);
        var storedTenant = reader.GetString(1);

        Assert.Equal(customerId.Value, storedCustomer);
        Assert.Equal(tenantId.Value, storedTenant);
    }

    [Fact]
    public async Task Convention_SupportsQueryingByTypedId()
    {
        var (connection, options) = await OpenAsync();
        await using var _ = connection;

        var target = OrderId.New();
        var other = OrderId.New();

        await using (var ctx = new ConventionDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            // Populate every id column. A string-backed id left at default has a null Value, and EF's
            // snapshot comparer invokes the generated Equals during change detection, so a degenerate
            // null-keyed row is not a meaningful fixture here.
            ctx.Orders.Add(new Order
            {
                Id = target,
                CustomerId = CustomerId.New(),
                TenantId = TenantId.New(),
                Description = "target",
            });
            ctx.Orders.Add(new Order
            {
                Id = other,
                CustomerId = CustomerId.New(),
                TenantId = TenantId.New(),
                Description = "other",
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new ConventionDbContext(options))
        {
            // A WHERE on the typed id must translate through the converter to a parameterized query.
            var found = await ctx.Orders.SingleAsync(o => o.Id == target);
            Assert.Equal("target", found.Description);
        }
    }
}
