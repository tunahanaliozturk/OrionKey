using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Moongazing.OrionKey.IntegrationTests;

public class NewStrategyIntegrationTests
{
    [Fact]
    public void AccountId_Cuid2_ShouldRoundTripThroughJson()
    {
        var original = AccountId.New();
        var restored = JsonSerializer.Deserialize<AccountId>(JsonSerializer.Serialize(original));
        Assert.Equal(original, restored);
    }

    [Fact]
    public void EventId_Ksuid_ShouldRoundTripThroughJson()
    {
        var original = EventId.New();
        var restored = JsonSerializer.Deserialize<EventId>(JsonSerializer.Serialize(original));
        Assert.Equal(original, restored);
    }

    [Fact]
    public void DocumentId_ObjectId_ShouldSerializeAsRawString()
    {
        var id = new DocumentId("507f1f77bcf86cd799439011");
        Assert.Equal("\"507f1f77bcf86cd799439011\"", JsonSerializer.Serialize(id));
    }

    [Fact]
    public void InvoiceId_SequentialGuid_ShouldRoundTripThroughJson()
    {
        var original = InvoiceId.New();
        var restored = JsonSerializer.Deserialize<InvoiceId>(JsonSerializer.Serialize(original));
        Assert.Equal(original, restored);
    }

    private sealed class Event
    {
        public EventId Id { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<Event> Events => Set<Event>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Event>().HasKey(e => e.Id);
            modelBuilder.Entity<Event>().Property(e => e.Id)
                .HasConversion(new EventIdValueConverter());
        }
    }

    [Fact]
    public async Task Event_Ksuid_ShouldPersistAndReload_WithGeneratedValueConverter()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;

        var id = EventId.New();
        await using (var ctx = new TestDbContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Events.Add(new Event { Id = id, Name = "Launch" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new TestDbContext(options))
        {
            var reloaded = await ctx.Events.SingleAsync();
            Assert.Equal(id, reloaded.Id);
            Assert.Equal("Launch", reloaded.Name);
        }
    }

    [Fact]
    public async Task Route_ShouldBindInvoiceId_ViaIParsable()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.MapGet("/invoices/{id}", (InvoiceId id) => Results.Ok(id.ToString()));
        await app.StartAsync();

        var client = app.GetTestClient();
        var id = InvoiceId.New();
        var response = await client.GetAsync($"/invoices/{id}");
        response.EnsureSuccessStatusCode();
        Assert.Contains(id.ToString(), await response.Content.ReadAsStringAsync());

        await app.StopAsync();
    }
}
