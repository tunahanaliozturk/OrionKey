using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Moongazing.OrionKey.IntegrationTests;

public class EfCoreRoundTripTests
{
    private sealed class Order
    {
        public OrderId Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>().HasKey(o => o.Id);
            modelBuilder.Entity<Order>().Property(o => o.Id)
                .HasConversion(new OrderIdValueConverter());
        }
    }

    [Fact]
    public async Task Order_ShouldPersistAndReload_WithGeneratedValueConverter()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;

        var id = OrderId.New();
        await using (var ctx = new TestDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Orders.Add(new Order { Id = id, Name = "Widget" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TestDbContext(options))
        {
            var reloaded = await ctx.Orders.SingleAsync();
            Assert.Equal(id, reloaded.Id);
            Assert.Equal("Widget", reloaded.Name);
        }
    }
}
