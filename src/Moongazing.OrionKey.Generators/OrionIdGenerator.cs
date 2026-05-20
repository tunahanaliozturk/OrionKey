using Microsoft.CodeAnalysis;
using Moongazing.OrionKey.Generators.Parsing;

namespace Moongazing.OrionKey.Generators;

/// <summary>
/// Incremental generator that turns <c>[OrionId]</c>-decorated structs into fully-featured
/// strongly-typed ids.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class OrionIdGenerator : IIncrementalGenerator
{
    private const string OneArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`1";
    private const string TwoArgAttribute = "Moongazing.OrionKey.OrionIdAttribute`2";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var oneArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            OneArgAttribute,
            predicate: static (_, _) => true,
            transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        var twoArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            TwoArgAttribute,
            predicate: static (_, _) => true,
            transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol);

        context.RegisterSourceOutput(oneArg, static (spc, symbol) => Handle(spc, symbol));
        context.RegisterSourceOutput(twoArg, static (spc, symbol) => Handle(spc, symbol));
    }

    private static void Handle(SourceProductionContext spc, INamedTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }

        if (!OrionIdParser.TryParse(symbol, out var model, out var diagnostics))
        {
            foreach (var diagnostic in diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }
            return;
        }

        spc.AddSource($"{model!.Name}.OrionId.g.cs", Emit.CoreBodyEmitter.Emit(model));

        var comparable = Emit.ComparableEmitter.Emit(model);
        if (comparable is not null)
        {
            spc.AddSource($"{model.Name}.OrionId.Comparable.g.cs", comparable);
        }

        spc.AddSource($"{model.Name}.OrionId.Json.g.cs", Emit.JsonConverterEmitter.Emit(model));
        spc.AddSource($"{model.Name}.OrionId.TypeConverter.g.cs", Emit.TypeConverterEmitter.Emit(model));
    }
}
