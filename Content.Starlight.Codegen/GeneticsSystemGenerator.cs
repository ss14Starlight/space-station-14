using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Content.Starlight.Codegen;

[Generator]
public sealed class GeneticsSystemGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Content.Shared.Genetics.GeneticComponentAttribute";

    // Each target the generator can emit into: (namespace, class name, class modifiers).
    // The generator will produce output for whichever targets are defined in the
    // current compilation's source, so Content.Shared gets SharedGeneticsSystem
    // and Content.Server gets GeneticsSystem.
    private static readonly (string Namespace, string ClassName, string Modifiers)[] Targets =
    {
        ("Content.Shared.Genetics", "SharedGeneticsSystem", "public abstract partial class"),
        ("Content.Server.Genetics", "GeneticsSystem",       "public sealed partial class"),
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var geneticClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => (Class: (INamedTypeSymbol)ctx.TargetSymbol, Attribute: ctx.Attributes[0])
            )
            .Collect();

        var combined = context.CompilationProvider.Combine(geneticClasses);

        context.RegisterSourceOutput(
            combined,
            (spc, source) => Execute(spc, source.Left, source.Right)
        );
    }

    private void Execute(SourceProductionContext context, Compilation compilation, ImmutableArray<(INamedTypeSymbol Class, AttributeData Attribute)> classes)
    {
        foreach (var (ns, className, modifiers) in Targets)
        {
            var targetSymbol = compilation.GetTypeByMetadataName($"{ns}.{className}");

            // Only generate if the partial class is defined in this compilation's
            // source (not in a referenced assembly).
            if (targetSymbol == null || targetSymbol.DeclaringSyntaxReferences.Length == 0)
                continue;

            var source = GenerateGeneticsClass(ns, className, modifiers, classes);
            context.AddSource($"{ns}.{className}.g.cs", source);
        }
    }

    private string GenerateGeneticsClass(
        string targetNamespace,
        string targetClassName,
        string targetModifiers,
        ImmutableArray<(INamedTypeSymbol Class, AttributeData Attribute)> allComponents)
    {
        var sb = new StringBuilder();

        // Add necessary using statements
        sb.AppendLine("using Robust.Shared.GameObjects;");

        // Collect unique namespaces from component types
        var namespaces = allComponents
            .Select(c => c.Class.ContainingNamespace.ToDisplayString())
            .Distinct()
            .OrderBy(ns => ns);

        foreach (var ns in namespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine($"{targetModifiers} {targetClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    private void InitializeGenerated()");
        sb.AppendLine("    {");
        sb.AppendLine("");
        foreach (var componentDef in allComponents)
        {
            sb.AppendLine($"        SubscribeLocalEvent<{componentDef.Class.Name}, ComponentInit>(OnComponentInit{componentDef.Class.Name});");
        }
        sb.AppendLine("");

        // Populate the genetic components table with type, complexity, and stability
        foreach (var componentDef in allComponents)
        {
            var complexity = GetAttributeArg(componentDef.Attribute, 0, 2);
            var stability = GetAttributeArg(componentDef.Attribute, 1, 1);
            sb.AppendLine($"        GeneticComponents[typeof({componentDef.Class.Name})] = new GeneticComponentInfo(typeof({componentDef.Class.Name}), {complexity}, {stability});");
        }

        sb.AppendLine("    }");
        sb.AppendLine("");
        foreach (var componentDef in allComponents)
        {
            sb.AppendLine($"    private void OnComponentInit{componentDef.Class.Name}(Entity<{componentDef.Class.Name}> ent, ref ComponentInit args)");
            sb.AppendLine("    {");
            sb.AppendLine($"        OnGeneticComponentAdded(ent.Owner, typeof({componentDef.Class.Name}));");
            sb.AppendLine("    }");
            sb.AppendLine("");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Reads a constructor argument from an AttributeData, falling back to a default value
    /// if the argument is missing.
    /// </summary>
    private static int GetAttributeArg(AttributeData attribute, int index, int defaultValue)
    {
        if (attribute.ConstructorArguments.Length > index
            && attribute.ConstructorArguments[index].Value is int value)
        {
            return value;
        }

        return defaultValue;
    }
}
