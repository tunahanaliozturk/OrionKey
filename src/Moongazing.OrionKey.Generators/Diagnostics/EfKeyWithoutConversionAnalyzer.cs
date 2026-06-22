using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Moongazing.OrionKey.Generators.Diagnostics;

/// <summary>
/// ORIONKEY006. Flags properties of an OrionId type that participate in EF Core
/// (entity has a DbSet or IEntityTypeConfiguration) but where no HasConversion /
/// HasOrionKeyConversion call wires the OrionKey value converter.
/// </summary>
/// <remarks>
/// Detection heuristic (documented for users who want to suppress / understand misses):
/// <list type="number">
///   <item>An entity is any class T where either (a) some DbContext-derived class exposes
///         <c>DbSet&lt;T&gt;</c>, or (b) some class implements
///         <c>IEntityTypeConfiguration&lt;T&gt;</c>.</item>
///   <item>For each OrionId-typed instance property on such an entity we scan every
///         <c>IEntityTypeConfiguration&lt;T&gt;.Configure</c> method body in the compilation
///         for a <c>HasConversion</c> or <c>HasOrionKeyConversion</c> call whose receiver
///         chain references a property of that name.</item>
///   <item>If no configuration class exists at all, we do not warn - the user may be wiring
///         conversions through model-builder conventions we cannot reach. This keeps false
///         positives at zero in non-trivial setups.</item>
///   <item>If a model-wide registration (<c>modelBuilder.UseOrionKeyConversions()</c> or
///         <c>configurationBuilder.ConfigureOrionKeyConversions(...)</c>) is present anywhere in the
///         compilation, every OrionId property is covered at once and we do not warn - a per-property
///         <c>HasConversion</c> is not required alongside it.</item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfKeyWithoutConversionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(OrionKeyDiagnostics.EfKeyWithoutConversion);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext start)
    {
        if (!OrionIdTypeIndex.ReferencesOrionKey(start.Compilation))
        {
            return;
        }

        var dbContextBase = start.Compilation.GetTypeByMetadataName(
            "Microsoft.EntityFrameworkCore.DbContext");
        var entityConfigurationOpenGeneric = start.Compilation.GetTypeByMetadataName(
            "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1");
        var dbSetOpenGeneric = start.Compilation.GetTypeByMetadataName(
            "Microsoft.EntityFrameworkCore.DbSet`1");

        if (entityConfigurationOpenGeneric is null || dbSetOpenGeneric is null)
        {
            return;
        }

        var entityTypes = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);
        var convertedProperties = new ConcurrentDictionary<(INamedTypeSymbol Entity, string Property), byte>();
        var configurationCounter = new ConfigurationCounter();

        // A single model-wide registration (modelBuilder.UseOrionKeyConversions() in OnModelCreating, or
        // configurationBuilder.ConfigureOrionKeyConversions(...) in ConfigureConventions) wires the
        // converter for every [OrionId] property on the model. When one is present anywhere in the
        // compilation, no per-property HasConversion is required, so ORIONKEY006 must not fire.
        var modelWideRegistration = new ConfigurationCounter();
        start.RegisterSyntaxNodeAction(
            nodeCtx => DetectModelWideRegistration(nodeCtx, modelWideRegistration),
            SyntaxKind.InvocationExpression);

        start.RegisterSymbolAction(symbolCtx =>
        {
            var named = (INamedTypeSymbol)symbolCtx.Symbol;

            foreach (var iface in named.AllInterfaces)
            {
                if (iface.IsGenericType
                    && SymbolEqualityComparer.Default.Equals(
                        iface.OriginalDefinition, entityConfigurationOpenGeneric))
                {
                    if (iface.TypeArguments[0] is INamedTypeSymbol entity)
                    {
                        entityTypes[entity] = 1;
                        configurationCounter.Mark();
                        CollectConvertedProperties(named, entity, convertedProperties);
                    }
                }
            }

            if (dbContextBase is not null && InheritsFrom(named, dbContextBase))
            {
                foreach (var member in named.GetMembers())
                {
                    ITypeSymbol? memberType = member switch
                    {
                        IPropertySymbol p => p.Type,
                        IFieldSymbol f => f.Type,
                        _ => null,
                    };
                    if (memberType is INamedTypeSymbol nt
                        && nt.IsGenericType
                        && SymbolEqualityComparer.Default.Equals(
                            nt.OriginalDefinition, dbSetOpenGeneric)
                        && nt.TypeArguments[0] is INamedTypeSymbol entity)
                    {
                        entityTypes[entity] = 1;
                    }
                }
            }
        }, SymbolKind.NamedType);

        start.RegisterCompilationEndAction(end =>
        {
            // A model-wide registration covers every OrionId property at once, so a per-property
            // HasConversion is not needed. Stay silent across the whole compilation when one is present.
            if (modelWideRegistration.HasAny)
            {
                return;
            }

            // If no IEntityTypeConfiguration<T> exists anywhere, conversions may be wired
            // via model-builder conventions we cannot reach. Stay silent to avoid noise.
            if (!configurationCounter.HasAny)
            {
                return;
            }

            foreach (var entityEntry in entityTypes)
            {
                var entity = entityEntry.Key;
                foreach (var member in entity.GetMembers())
                {
                    if (member is not IPropertySymbol property)
                    {
                        continue;
                    }
                    if (property.IsStatic || property.IsIndexer)
                    {
                        continue;
                    }
                    if (property.Type is not INamedTypeSymbol typeSymbol)
                    {
                        continue;
                    }
                    if (!OrionIdTypeIndex.IsOrionIdType(typeSymbol))
                    {
                        continue;
                    }
                    if (convertedProperties.ContainsKey((entity, property.Name)))
                    {
                        continue;
                    }

                    var location = property.Locations.FirstOrDefault() ?? entity.Locations.FirstOrDefault();
                    if (location is null)
                    {
                        continue;
                    }

                    end.ReportDiagnostic(Diagnostic.Create(
                        OrionKeyDiagnostics.EfKeyWithoutConversion,
                        location,
                        entity.Name,
                        property.Name,
                        typeSymbol.Name));
                }
            }
        });
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }
        return false;
    }

    private static void CollectConvertedProperties(
        INamedTypeSymbol configurationClass,
        INamedTypeSymbol entity,
        ConcurrentDictionary<(INamedTypeSymbol Entity, string Property), byte> sink)
    {
        foreach (var declRef in configurationClass.DeclaringSyntaxReferences)
        {
            if (declRef.GetSyntax() is not ClassDeclarationSyntax classSyntax)
            {
                continue;
            }

            foreach (var method in classSyntax.Members.OfType<MethodDeclarationSyntax>())
            {
                if (method.Identifier.ValueText != "Configure")
                {
                    continue;
                }

                var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                if (body is null)
                {
                    continue;
                }

                foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax member)
                    {
                        continue;
                    }

                    var calledName = member.Name.Identifier.ValueText;
                    if (calledName is not "HasConversion" and not "HasOrionKeyConversion")
                    {
                        continue;
                    }

                    foreach (var propertyName in ExtractTargetPropertyNames(member.Expression))
                    {
                        sink[(entity, propertyName)] = 1;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recognizes a model-wide OrionKey converter registration -
    /// <c>UseOrionKeyConversions()</c> or <c>ConfigureOrionKeyConversions(...)</c> - and records its
    /// presence. Detection is by method name (mirroring the syntactic name match used for
    /// <c>HasConversion</c> / <c>HasOrionKeyConversion</c>); when the symbol binds, the call is
    /// additionally required to resolve to OrionKey's EF Core extensions namespace so an unrelated
    /// method of the same name does not suppress the diagnostic. When the symbol cannot be resolved
    /// (the OrionKey EF Core assembly is not referenced by this compilation), the name match alone is
    /// honored.
    /// </summary>
    private static void DetectModelWideRegistration(
        SyntaxNodeAnalysisContext context,
        ConfigurationCounter sink)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var calledName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        if (calledName is not "UseOrionKeyConversions" and not "ConfigureOrionKeyConversions")
        {
            return;
        }

        // When the call binds to a symbol, require it to live in OrionKey's EF Core extensions
        // namespace. If it does not bind (the EF Core package is not referenced here), the highly
        // specific name match stands on its own.
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is IMethodSymbol method
            && !IsOrionKeyEntityFrameworkCoreExtension(method.ContainingType))
        {
            return;
        }

        sink.Mark();
    }

    private static bool IsOrionKeyEntityFrameworkCoreExtension(INamedTypeSymbol? containingType)
        => containingType?.ContainingNamespace?.ToDisplayString()
            == "Moongazing.OrionKey.EntityFrameworkCore";

    private static IEnumerable<string> ExtractTargetPropertyNames(ExpressionSyntax receiver)
    {
        foreach (var invocation in receiver.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax m)
            {
                continue;
            }
            if (m.Name.Identifier.ValueText != "Property")
            {
                continue;
            }

            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    yield return literal.Token.ValueText;
                    continue;
                }
                if (arg.Expression is SimpleLambdaExpressionSyntax lambda
                    && lambda.Body is MemberAccessExpressionSyntax lambdaMember)
                {
                    yield return lambdaMember.Name.Identifier.ValueText;
                }
            }
        }
    }

    private sealed class ConfigurationCounter
    {
        private int flag;
        public bool HasAny => Volatile.Read(ref flag) != 0;
        public void Mark() => Interlocked.Exchange(ref flag, 1);
    }
}
