using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongazing.OrionKey.Generators.Model;
using Moongazing.OrionKey.Generators.Parsing;

namespace Moongazing.OrionKey.Generators.Tests;

public class ParserTests
{
    private static INamedTypeSymbol FirstStruct(string source, out Compilation compilation)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        _ = typeof(global::Moongazing.OrionKey.OrionIdAttribute<>).Assembly;
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location));
        compilation = CSharpCompilation.Create("ParseTest", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var root = tree.GetRoot();
        var structSyntax = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>().First();
        return (INamedTypeSymbol)compilation.GetSemanticModel(tree).GetDeclaredSymbol(structSyntax)!;
    }

    [Fact]
    public void Parse_ShouldResolveGuidValueType_AndNoneStrategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid>] public readonly partial struct OrderId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out var diagnostics);

        Assert.True(ok);
        Assert.Equal(ValueType.Guid, model!.ValueType);
        Assert.Equal(StrategyType.None, model.Strategy);
        Assert.Equal("OrderId", model.Name);
        Assert.Equal("Demo", model.Namespace);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ShouldResolveSnowflakeStrategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<long, Snowflake>] public readonly partial struct UserId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out _);

        Assert.True(ok);
        Assert.Equal(ValueType.Int64, model!.ValueType);
        Assert.Equal(StrategyType.Snowflake, model.Strategy);
        Assert.True(model.GeneratesNew);
        Assert.True(model.IsSortable);
    }

    [Fact]
    public void Parse_ShouldMarkIntAsExternallyAssigned()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<int>] public readonly partial struct LineId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out _);

        Assert.True(ok);
        Assert.False(model!.GeneratesNew);
        Assert.False(model.IsSortable);
    }

    [Fact]
    public void Parse_ShouldResolveCuid2Strategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Cuid2>] public readonly partial struct AccountId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out var diagnostics);

        Assert.True(ok);
        Assert.Equal(StrategyType.Cuid2, model!.Strategy);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ShouldResolveSequentialGuidStrategy()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid, SequentialGuid>] public readonly partial struct InvoiceId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out var model, out var diagnostics);

        Assert.True(ok);
        Assert.Equal(StrategyType.SequentialGuid, model!.Strategy);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ShouldResolveKsuidAndObjectIdStrategies()
    {
        var ksuid = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, Ksuid>] public readonly partial struct EventId;
            """, out _);
        var objectId = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<string, ObjectId>] public readonly partial struct DocumentId;
            """, out _);

        Assert.True(OrionIdParser.TryParse(ksuid, out var km, out _));
        Assert.True(OrionIdParser.TryParse(objectId, out var om, out _));
        Assert.Equal(StrategyType.Ksuid, km!.Strategy);
        Assert.Equal(StrategyType.ObjectId, om!.Strategy);
    }

    [Fact]
    public void Parse_ShouldRejectCuid2WithGuidValue()
    {
        var symbol = FirstStruct("""
            using Moongazing.OrionKey;
            namespace Demo;
            [OrionId<System.Guid, Cuid2>] public readonly partial struct BadId;
            """, out _);

        var ok = OrionIdParser.TryParse(symbol, out _, out var diagnostics);

        Assert.False(ok);
        Assert.Contains(diagnostics, d => d.Id == "ORIONKEY004");
    }
}
