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

        var flags = context.CompilationProvider.Select(static (compilation, _) => new IntegrationFlags(
            HasEfCore: compilation.GetTypeByMetadataName(
                "Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter`2") is not null,
            HasDapper: compilation.GetTypeByMetadataName(
                "Dapper.SqlMapper+TypeHandler`1") is not null,
            HasNewtonsoftJson: compilation.GetTypeByMetadataName(
                "Newtonsoft.Json.JsonConverter") is not null,
            HasMongo: compilation.GetTypeByMetadataName(
                "MongoDB.Bson.Serialization.Serializers.SerializerBase`1") is not null,
            HasSwashbuckle: compilation.GetTypeByMetadataName(
                "Swashbuckle.AspNetCore.SwaggerGen.ISchemaFilter") is not null));

        context.RegisterSourceOutput(oneArg.Combine(flags),
            static (spc, pair) => Handle(spc, pair.Left, pair.Right));
        context.RegisterSourceOutput(twoArg.Combine(flags),
            static (spc, pair) => Handle(spc, pair.Left, pair.Right));
    }

    private static void Handle(SourceProductionContext spc, INamedTypeSymbol? symbol, IntegrationFlags flags)
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

        foreach (var diagnostic in diagnostics)
        {
            spc.ReportDiagnostic(diagnostic);
        }

        spc.AddSource($"{model!.Name}.OrionId.g.cs", Emit.CoreBodyEmitter.Emit(model));

        var comparable = Emit.ComparableEmitter.Emit(model);
        if (comparable is not null)
        {
            spc.AddSource($"{model.Name}.OrionId.Comparable.g.cs", comparable);
        }

        spc.AddSource($"{model.Name}.OrionId.Json.g.cs", Emit.JsonConverterEmitter.Emit(model));
        spc.AddSource($"{model.Name}.OrionId.TypeConverter.g.cs", Emit.TypeConverterEmitter.Emit(model));
        spc.AddSource($"{model.Name}.OrionId.Parsable.g.cs", Emit.ParsableEmitter.Emit(model));

        if (flags.HasEfCore)
        {
            spc.AddSource($"{model.Name}.OrionId.EfCore.g.cs", Emit.EfCoreConverterEmitter.Emit(model));
        }

        if (flags.HasDapper)
        {
            spc.AddSource($"{model.Name}.OrionId.Dapper.g.cs", Emit.DapperTypeHandlerEmitter.Emit(model));
        }

        if (flags.HasNewtonsoftJson)
        {
            spc.AddSource($"{model.Name}.OrionId.NewtonsoftJson.g.cs",
                Emit.NewtonsoftJsonConverterEmitter.Emit(model));
        }

        if (flags.HasMongo)
        {
            spc.AddSource($"{model.Name}.OrionId.Mongo.g.cs",
                Emit.MongoBsonSerializerEmitter.Emit(model));
        }
    }
}
