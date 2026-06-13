using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Moongazing.OrionKey.Generators.Model;
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
        // v0.5.5 perf pass:
        //   * predicate narrows to StructDeclarationSyntax up-front so attribute discovery
        //     does not walk into class/record/interface candidates.
        //   * transform parses the symbol ONCE into the cache-friendly ParsedOrionId
        //     (value-equatable) instead of returning INamedTypeSymbol (reference-identity,
        //     rebuilt every keystroke so the cache was permanently cold).
        var oneArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            OneArgAttribute,
            // Include both plain struct declarations and record struct declarations - record
// structs also pass [AttributeUsage(AttributeTargets.Struct)] so a user-annotated
// `readonly partial record struct UserId { }` previously slipped through the
// transform and surfaced as an ORIONKEY001 (not-readonly-partial-struct) noise
// diagnostic. RecordStructDeclarationSyntax IS-A TypeDeclarationSyntax but not
// StructDeclarationSyntax, so it has to be added explicitly.
predicate: static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ctx.TargetSymbol is INamedTypeSymbol s
                ? OrionIdParser.Parse(s)
                : default)
            .WithTrackingName("OrionId_OneArg_Parse");

        var twoArg = context.SyntaxProvider.ForAttributeWithMetadataName(
            TwoArgAttribute,
            // Include both plain struct declarations and record struct declarations - record
// structs also pass [AttributeUsage(AttributeTargets.Struct)] so a user-annotated
// `readonly partial record struct UserId { }` previously slipped through the
// transform and surfaced as an ORIONKEY001 (not-readonly-partial-struct) noise
// diagnostic. RecordStructDeclarationSyntax IS-A TypeDeclarationSyntax but not
// StructDeclarationSyntax, so it has to be added explicitly.
predicate: static (node, _) => node is StructDeclarationSyntax or RecordDeclarationSyntax,
            transform: static (ctx, _) => ctx.TargetSymbol is INamedTypeSymbol s
                ? OrionIdParser.Parse(s)
                : default)
            .WithTrackingName("OrionId_TwoArg_Parse");

        // IntegrationFlags is a value record so this pipeline step is cache-stable across
        // recompiles whenever the consumer's referenced-assembly set is unchanged.
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

        // Registrar emit reuses the SAME parsed output - no second parse pass over symbols.
        // EquatableArray<OrionIdModel> gives the collect step structural equality so an
        // unrelated edit to one OrionId struct does not bust the registrar's cache.
        var oneArgModels = oneArg.Select(static (parsed, _) => parsed.Model).Where(static m => m is not null);
        var twoArgModels = twoArg.Select(static (parsed, _) => parsed.Model).Where(static m => m is not null);

        var allModels = oneArgModels.Collect()
            .Combine(twoArgModels.Collect())
            .Select(static (pair, _) =>
            {
                var combined = pair.Left.AddRange(pair.Right);
                var nonNull = ImmutableArray.CreateBuilder<OrionIdModel>(combined.Length);
                foreach (var model in combined)
                {
                    if (model is not null)
                    {
                        nonNull.Add(model);
                    }
                }
                return new EquatableArray<OrionIdModel>(nonNull.ToImmutable());
            })
            .WithTrackingName("OrionId_AllModels");

        context.RegisterSourceOutput(allModels.Combine(flags),
            static (spc, pair) => EmitRegistrars(spc, pair.Left, pair.Right));
    }

    private static void Handle(SourceProductionContext spc, ParsedOrionId parsed, IntegrationFlags flags)
    {
        foreach (var diag in parsed.Diagnostics)
        {
            spc.ReportDiagnostic(diag.ToDiagnostic());
        }

        var model = parsed.Model;
        if (model is null)
        {
            return;
        }

        spc.AddSource($"{model.Name}.OrionId.g.cs", Emit.CoreBodyEmitter.Emit(model));

        // v0.5.26: IComparable is now emitted for EVERY id type (was sortable-only).
        spc.AddSource($"{model.Name}.OrionId.Comparable.g.cs", Emit.ComparableEmitter.Emit(model));

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

        if (flags.HasSwashbuckle)
        {
            spc.AddSource($"{model.Name}.OrionId.OpenApi.g.cs",
                Emit.OpenApiSchemaFilterEmitter.Emit(model));
        }
    }

    private static void EmitRegistrars(
        SourceProductionContext spc,
        EquatableArray<OrionIdModel> models,
        IntegrationFlags flags)
    {
        if (models.Count == 0)
        {
            return;
        }

        // Existing emitters consume IReadOnlyList<OrionIdModel>; the array view satisfies
        // that without copying.
        var list = (IReadOnlyList<OrionIdModel>)models.AsImmutableArray();

        if (flags.HasDapper)
        {
            spc.AddSource("OrionKeyDapperRegistrar.g.cs", Emit.RegistrationEmitter.EmitDapper(list));
        }
        if (flags.HasNewtonsoftJson)
        {
            spc.AddSource("OrionKeyNewtonsoftJsonRegistrar.g.cs",
                Emit.RegistrationEmitter.EmitNewtonsoftJson(list));
        }
        if (flags.HasMongo)
        {
            spc.AddSource("OrionKeyMongoRegistrar.g.cs", Emit.RegistrationEmitter.EmitMongo(list));
        }
        if (flags.HasSwashbuckle)
        {
            spc.AddSource("OrionKeyOpenApiRegistrar.g.cs", Emit.RegistrationEmitter.EmitOpenApi(list));
        }
    }
}
