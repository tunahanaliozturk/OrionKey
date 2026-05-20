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
}
