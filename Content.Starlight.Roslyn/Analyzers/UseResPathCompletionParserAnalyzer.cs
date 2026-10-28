using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Content.Starlight.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseResPathCompletionParserAnalyzer : DiagnosticAnalyzer
{
    private const string CommandArgumentAttribute = "Robust.Shared.Toolshed.CommandArgumentAttribute";
    private const string CommandImplementationAttribute = "Robust.Shared.Toolshed.CommandImplementationAttribute";
    private const string PipedArgumentAttribute = "Robust.Shared.Toolshed.PipedArgumentAttribute";
    private const string CommandInvertedAttribute = "Robust.Shared.Toolshed.CommandInvertedAttribute";
    private const string CommandInvocationContextAttribute = "Robust.Shared.Toolshed.CommandInvocationContextAttribute";
    private const string ResPath = "Robust.Shared.Utility.ResPath";
    private const string CustomCompletionParser = "Robust.Shared.Toolshed.TypeParsers.CustomCompletionParser`1";

#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor _diagnostic = new(DiagnosticCodes.UseResPathCompletionParser,
#pragma warning restore RS2008
        "Use ResPath Completion Parser",
        "Use [CommandArgument] with a completion parser for ResPath for toolshed argument", "Usage",
        DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_diagnostic];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(ctx =>
        {
            var compilation = ctx.Compilation;

            if (compilation.GetTypeByMetadataName(CommandArgumentAttribute) is not { } argAttr
                || compilation.GetTypeByMetadataName(CommandImplementationAttribute) is not { } implAttr
                || compilation.GetTypeByMetadataName(ResPath) is not { } resPath
                || compilation.GetTypeByMetadataName(CustomCompletionParser) is not { } completionParser)
                return;

            // CustomCompletionParser<ResPath>
            var resPathParser = completionParser.Construct(resPath);

            // Parameters with these attributes aren't parsed from user input, so completions don't apply.
            var nonArgAttrs =
                new[] { PipedArgumentAttribute, CommandInvertedAttribute, CommandInvocationContextAttribute }
                    .Select(compilation.GetTypeByMetadataName).ToImmutableArray();

            ctx.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeNode(nodeContext, argAttr, implAttr, resPath, resPathParser, nonArgAttrs),
                SyntaxKind.Parameter);
        });
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext ctx, INamedTypeSymbol argAttrSymbol,
        INamedTypeSymbol implAttrSymbol, INamedTypeSymbol resPathSymbol, INamedTypeSymbol resPathParserSymbol,
        ImmutableArray<INamedTypeSymbol> nonArgAttrSymbols)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ctx.CancellationToken) is not IParameterSymbol param) return;
        if (!IsResPath(param.Type, resPathSymbol)) return;

        if (param.ContainingSymbol is not IMethodSymbol method) return;
        if (!method.GetAttributes()
                .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, implAttrSymbol))) return;

        var attrs = param.GetAttributes();
        if (attrs.Any(a => nonArgAttrSymbols.Any(s => SymbolEqualityComparer.Default.Equals(a.AttributeClass, s))))
            return;

        var argAttr = attrs.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, argAttrSymbol));

        // Enforce it has a completion parser for ResPaths
        if (argAttr is not null && argAttr.ConstructorArguments.Length > 0 &&
            argAttr.ConstructorArguments[0].Value is INamedTypeSymbol parser &&
            InheritsFrom(parser, resPathParserSymbol)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(_diagnostic, ctx.Node.GetLocation()));
    }

    private static bool IsResPath(ITypeSymbol type, INamedTypeSymbol resPathSymbol)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];

        return SymbolEqualityComparer.Default.Equals(type, resPathSymbol);
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;

        return false;
    }
}
