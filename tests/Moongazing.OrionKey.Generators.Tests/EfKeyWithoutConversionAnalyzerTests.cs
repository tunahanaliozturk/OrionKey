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
