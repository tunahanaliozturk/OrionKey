namespace Moongazing.OrionKey.EntityFrameworkCore.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Covers the per-property generic helper, the converter factory, the pre-convention path, and the
/// rule that an explicit converter is never clobbered by the convention.
/// </summary>
public sealed class ConverterAndHelperTests
{
    [Fact]
    public void Factory_Generic_RoundTripsGuidIdThroughValue()
    {
        var converter = OrionKeyValueConverterFactory.Create<OrderId, Guid>();

        var id = OrderId.New();
        var provider = (Guid)converter.ConvertToProvider(id)!;
        var model = (OrderId)converter.ConvertFromProvider(provider)!;

        Assert.Equal(id.Value, provider);
        Assert.Equal(id, model);
    }

    [Fact]
    public void Factory_Generic_RoundTripsStringId()
    {
        var converter = OrionKeyValueConverterFactory.Create<TenantId, string>();

        var id = TenantId.New();
        var provider = (string)converter.ConvertToProvider(id)!;
        var model = (TenantId)converter.ConvertFromProvider(provider)!;

        Assert.Equal(id.Value, provider);
        Assert.Equal(id, model);
    }

    [Fact]
    public void Factory_NonGeneric_ResolvesValueTypeFromIdType()
    {
        var converter = OrionKeyValueConverterFactory.Create(typeof(CustomerId));

        Assert.Equal(typeof(CustomerId), converter.ModelClrType);
        Assert.Equal(typeof(long), converter.ProviderClrType);

        var id = CustomerId.New();
        var provider = (long)converter.ConvertToProvider(id)!;
        Assert.Equal(id.Value, provider);
    }

    [Fact]
    public void Factory_NonGeneric_Throws_ForNonOrionKeyType()
    {
        // string has no public (string) ctor + Value getter shape, but more importantly it is the kind
        // of arbitrary type the convention must never have handed to the factory. Guard the contract.
        Assert.Throws<ArgumentException>(() => OrionKeyValueConverterFactory.Create(typeof(string)));
    }

    private sealed class Widget
    {
        public OrderId Id { get; set; }
    }

    private sealed class ExplicitConverterContext(DbContextOptions<ExplicitConverterContext> options)
        : DbContext(options)
    {
        public static readonly ValueConverter<OrderId, Guid> Sentinel =
            new(id => id.Value, value => new OrderId(value));

        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>().HasKey(w => w.Id);

            // Explicit converter set BEFORE the convention runs. The convention must leave it in place.
            modelBuilder.Entity<Widget>().Property(w => w.Id).HasConversion(Sentinel);

            modelBuilder.UseOrionKeyConversions();
        }
    }

    [Fact]
    public void Convention_DoesNotOverride_ExplicitlyConfiguredConverter()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ExplicitConverterContext>()
            .UseSqlite(connection)
            .Options;

        using var ctx = new ExplicitConverterContext(options);
        var converter = ctx.Model
            .FindEntityType(typeof(Widget))!
            .FindProperty(nameof(Widget.Id))!
            .GetValueConverter();

        Assert.Same(ExplicitConverterContext.Sentinel, converter);
    }

    [Fact]
    public void PerProperty_GenericHelper_WiresConverter()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<PerPropertyContext>()
            .UseSqlite(connection)
            .Options;

        using var ctx = new PerPropertyContext(options);
        var converter = ctx.Model
            .FindEntityType(typeof(Widget))!
            .FindProperty(nameof(Widget.Id))!
            .GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(typeof(OrderId), converter!.ModelClrType);
        Assert.Equal(typeof(Guid), converter.ProviderClrType);
    }

    private sealed class PerPropertyContext(DbContextOptions<PerPropertyContext> options)
        : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>().HasKey(w => w.Id);
            modelBuilder.Entity<Widget>().Property(w => w.Id)
                .HasOrionKeyConversion<OrderId, Guid>();
        }
    }

    private sealed class PreConventionContext(DbContextOptions<PreConventionContext> options)
        : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Widget>().HasKey(w => w.Id);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.ConfigureOrionKeyConversions(typeof(OrderId).Assembly);
    }

    [Fact]
    public async Task PreConvention_ConfigureOrionKeyConversions_RoundTrips()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PreConventionContext>()
            .UseSqlite(connection)
            .Options;

        var id = OrderId.New();
        await using (var ctx = new PreConventionContext(options))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Widgets.Add(new Widget { Id = id });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new PreConventionContext(options))
        {
            var reloaded = await ctx.Widgets.SingleAsync();
            Assert.Equal(id, reloaded.Id);
        }
    }
}
