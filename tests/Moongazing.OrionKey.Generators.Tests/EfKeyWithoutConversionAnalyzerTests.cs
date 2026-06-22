using System.Linq;
using System.Threading.Tasks;
using Moongazing.OrionKey.Generators.Diagnostics;

namespace Moongazing.OrionKey.Generators.Tests;

public class EfKeyWithoutConversionAnalyzerTests
{
    [Fact]
    public async Task ORIONKEY006_Fires_WhenEntityIdHasNoHasConversion()
    {
        // Entity with OrionId-typed key; configuration registers entity but no HasConversion call.
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class Order
            {
                public OrderId Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.Contains("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_DoesNotFire_WhenHasConversionLambdaPresent()
    {
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class Order
            {
                public OrderId Id { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                    builder.Property(x => x.Id).HasConversion(new OrderIdValueConverter());
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_DoesNotFire_WhenHasOrionKeyConversionPresent()
    {
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class Order
            {
                public OrderId Id { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                    builder.Property(x => x.Id).HasOrionKeyConversion();
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_DoesNotFire_WhenModelWideUseOrionKeyConversionsPresent()
    {
        // Entity configuration registers the entity but wires no per-property HasConversion. A single
        // modelBuilder.UseOrionKeyConversions() in OnModelCreating covers every OrionId property, so the
        // diagnostic must stay silent - otherwise the model-wide API is unusable under TreatWarningsAsErrors.
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;
            [OrionId<System.Guid>] public readonly partial struct CustomerId;

            public class Order
            {
                public OrderId Id { get; set; }
                public CustomerId CustomerId { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                }
            }

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders => Set<Order>();

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.ApplyConfiguration(new OrderConfiguration());
                    modelBuilder.UseOrionKeyConversions();
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_DoesNotFire_WhenModelWideConfigureOrionKeyConversionsPresent()
    {
        // The ConfigureConventions counterpart: configurationBuilder.ConfigureOrionKeyConversions(...)
        // is the other model-wide registration and must suppress ORIONKEY006 the same way.
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class Order
            {
                public OrderId Id { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                }
            }

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders => Set<Order>();

                protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
                {
                    configurationBuilder.ConfigureOrionKeyConversions();
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_StillFires_WhenNeitherPerPropertyNorModelWideConversionRegistered()
    {
        // Negative control for the model-wide suppression: with an entity configuration present but no
        // per-property HasConversion and no model-wide UseOrionKeyConversions / ConfigureOrionKeyConversions
        // call anywhere, the diagnostic must still fire. This guards against the suppression being too broad.
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class Order
            {
                public OrderId Id { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                }
            }

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders => Set<Order>();

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.ApplyConfiguration(new OrderConfiguration());
                    // No model-wide registration on purpose.
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.Contains("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_DoesNotFire_WhenStructIsNotOnAnEntity()
    {
        // OrderId is declared and used, but no EF Core IEntityTypeConfiguration is in scope.
        // The analyzer must stay silent rather than warn on every DTO.
        const string source = """
            using Moongazing.OrionKey;
            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;

            public class OrderDto
            {
                public OrderId Id { get; set; }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        Assert.DoesNotContain("ORIONKEY006", diags.Ids());
    }

    [Fact]
    public async Task ORIONKEY006_FiresForForeignKeyProperty_WhenNoConversionWired()
    {
        // Foreign key (CustomerId) on Order with no HasConversion either - still a key.
        const string source = """
            using Moongazing.OrionKey;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace Demo;

            [OrionId<System.Guid>] public readonly partial struct OrderId;
            [OrionId<System.Guid>] public readonly partial struct CustomerId;

            public class Order
            {
                public OrderId Id { get; set; }
                public CustomerId CustomerId { get; set; }
            }

            public class OrderConfiguration : IEntityTypeConfiguration<Order>
            {
                public void Configure(EntityTypeBuilder<Order> builder)
                {
                    builder.HasKey(x => x.Id);
                    builder.Property(x => x.Id).HasConversion(new OrderIdValueConverter());
                    // CustomerId left without HasConversion on purpose.
                }
            }
            """;

        var diags = await AnalyzerHarness.RunAsync(new EfKeyWithoutConversionAnalyzer(), source);
        var hits = diags.Where(d => d.Id == "ORIONKEY006").ToArray();
        Assert.Single(hits);
        Assert.Contains("CustomerId", hits[0].GetMessage(System.Globalization.CultureInfo.InvariantCulture));
    }
}
