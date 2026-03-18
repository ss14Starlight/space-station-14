using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Content.Starlight.Codegen;

[Generator]
public sealed class GeneticsSystemGenerator : IIncrementalGenerator
{
    private const string AttributeName = "Content.Shared.Genetics.GeneticComponentAttribute";
    private const string VariableAttrMetadataName = "GeneticMultiValueVariableAttribute`1";
    private const string VariableAttrNamespace = "Content.Shared.Genetics";

    // Each target the generator can emit into: (namespace, class name, class modifiers).
    // The generator will produce output for whichever targets are defined in the
    // current compilation's source, so Content.Shared gets SharedGeneticsSystem
    // and Content.Server gets GeneticsSystem.
    private static readonly (string Namespace, string ClassName, string Modifiers)[] Targets =
    {
        ("Content.Shared.Genetics", "SharedGeneticsSystem", "public abstract partial class"),
        ("Content.Server.Genetics", "GeneticsSystem",       "public sealed partial class"),
    };

    private sealed class VariableInfo
    {
        public readonly string MemberName;
        public readonly ITypeSymbol FieldType;
        public readonly TypedConstant DefaultValue;
        public readonly ImmutableArray<TypedConstant> Values;
        public readonly int CodonCount;
        public readonly int OffsetInVariableRegion;

        public VariableInfo(string memberName, ITypeSymbol fieldType, TypedConstant defaultValue, ImmutableArray<TypedConstant> values, int codonCount, int offsetInVariableRegion)
        {
            MemberName = memberName;
            FieldType = fieldType;
            DefaultValue = defaultValue;
            Values = values;
            CodonCount = codonCount;
            OffsetInVariableRegion = offsetInVariableRegion;
        }
    }

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

        // ── InitializeGenerated ──
        sb.AppendLine("    private void InitializeGenerated()");
        sb.AppendLine("    {");
        sb.AppendLine("");

        // Collect per-component variable info for later method generation
        var componentVariables = new Dictionary<string, List<VariableInfo>>();

        foreach (var componentDef in allComponents)
        {
            sb.AppendLine($"        SubscribeLocalEvent<{componentDef.Class.Name}, ComponentInit>(OnComponentInit{componentDef.Class.Name});");
        }
        sb.AppendLine("");

        foreach (var componentDef in allComponents)
        {
            var complexity = GetAttributeArg(componentDef.Attribute, 0, 2);
            var stability = GetAttributeArg(componentDef.Attribute, 1, 1);

            var variables = GetVariables(componentDef.Class);
            componentVariables[componentDef.Class.Name] = variables;

            var varCodonCount = variables.Sum(v => v.CodonCount);

            sb.AppendLine($"        GeneticComponents[typeof({componentDef.Class.Name})] = new GeneticComponentInfo(typeof({componentDef.Class.Name}), {complexity}, {stability}, {varCodonCount});");

            if (variables.Count > 0)
            {
                sb.AppendLine($"        VariableSyncWriteDna[typeof({componentDef.Class.Name})] = SyncWrite{componentDef.Class.Name}Variables;");
                sb.AppendLine($"        VariableSyncReadDna[typeof({componentDef.Class.Name})] = SyncRead{componentDef.Class.Name}Variables;");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("");

        // ── Per-component Init handlers ──
        foreach (var componentDef in allComponents)
        {
            sb.AppendLine($"    private void OnComponentInit{componentDef.Class.Name}(Entity<{componentDef.Class.Name}> ent, ref ComponentInit args)");
            sb.AppendLine("    {");
            sb.AppendLine($"        OnGeneticComponentAdded(ent.Owner, typeof({componentDef.Class.Name}));");
            sb.AppendLine("    }");
            sb.AppendLine("");
        }

        // ── Per-component variable sync methods ──
        foreach (var componentDef in allComponents)
        {
            var variables = componentVariables[componentDef.Class.Name];
            if (variables.Count == 0)
                continue;

            var className = componentDef.Class.Name;

            // SyncWrite: value → DNA (encode current field values into DNA variable codons)
            sb.AppendLine($"    private void SyncWrite{className}Variables(EntityUid uid, char[] chars, RoundGeneticRecord record)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (!EntityManager.TryGetComponent<{className}>(uid, out var comp))");
            sb.AppendLine("            return;");

            foreach (var v in variables)
            {
                var typeName = v.FieldType.ToDisplayString();
                var valuesLiteral = FormatValuesArray(v.FieldType, v.Values);

                sb.AppendLine($"        {{");
                sb.AppendLine($"            var values = new {typeName}[] {{ {valuesLiteral} }};");
                sb.AppendLine($"            var closest = 0;");
                sb.AppendLine($"            var bestDist = Math.Abs(({FormatDistanceType(v.FieldType)})comp.{v.MemberName} - ({FormatDistanceType(v.FieldType)})values[0]);");
                sb.AppendLine($"            for (var i = 1; i < values.Length; i++)");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                var dist = Math.Abs(({FormatDistanceType(v.FieldType)})comp.{v.MemberName} - ({FormatDistanceType(v.FieldType)})values[i]);");
                sb.AppendLine($"                if (dist < bestDist) {{ closest = i; bestDist = dist; }}");
                sb.AppendLine($"            }}");
                sb.AppendLine($"            WriteVariableMatches(chars, record, {v.OffsetInVariableRegion}, {v.CodonCount}, closest);");
                sb.AppendLine($"        }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("");

            // SyncRead: DNA → value (read DNA variable codons and apply to component fields)
            sb.AppendLine($"    private void SyncRead{className}Variables(EntityUid uid, string dna, RoundGeneticRecord record)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (!EntityManager.TryGetComponent<{className}>(uid, out var comp))");
            sb.AppendLine("            return;");

            foreach (var v in variables)
            {
                var typeName = v.FieldType.ToDisplayString();
                var valuesLiteral = FormatValuesArray(v.FieldType, v.Values);

                sb.AppendLine($"        {{");
                sb.AppendLine($"            var matches = CountVariableMatches(dna.AsSpan(), record, {v.OffsetInVariableRegion}, {v.CodonCount});");
                sb.AppendLine($"            var values = new {typeName}[] {{ {valuesLiteral} }};");
                sb.AppendLine($"            comp.{v.MemberName} = values[Math.Min(matches, values.Length - 1)];");
                sb.AppendLine($"        }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Scan members of a [GeneticComponent] class for [GeneticMultiValueVariable] attributes.
    /// </summary>
    private static List<VariableInfo> GetVariables(INamedTypeSymbol classSymbol)
    {
        var result = new List<VariableInfo>();
        var currentOffset = 0;

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not (IFieldSymbol or IPropertySymbol))
                continue;

            foreach (var attr in member.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass?.OriginalDefinition is not { } origDef)
                    continue;

                if (origDef.ContainingNamespace.ToDisplayString() != VariableAttrNamespace)
                    continue;
                if (origDef.MetadataName != VariableAttrMetadataName)
                    continue;

                var typeArg = attrClass.TypeArguments[0];
                var defaultValue = attr.ConstructorArguments[0];
                var valuesArray = attr.ConstructorArguments[1];
                var values = valuesArray.Values;
                var codonCount = values.Length > 0 ? values.Length - 1 : 0;

                result.Add(new VariableInfo(
                    member.Name,
                    typeArg,
                    defaultValue,
                    values,
                    codonCount,
                    currentOffset));

                currentOffset += codonCount;
            }
        }

        return result;
    }

    /// <summary>
    /// Format an array of TypedConstants as a C# array initializer.
    /// </summary>
    private static string FormatValuesArray(ITypeSymbol type, ImmutableArray<TypedConstant> values)
    {
        return string.Join(", ", values.Select(v => FormatLiteral(type, v)));
    }

    /// <summary>
    /// Format a single TypedConstant as a C# literal.
    /// </summary>
    private static string FormatLiteral(ITypeSymbol type, TypedConstant tc)
    {
        if (tc.Value is float f)
            return f.ToString("G", CultureInfo.InvariantCulture) + "f";
        if (tc.Value is double d)
            return d.ToString("G", CultureInfo.InvariantCulture) + "d";
        if (tc.Value is int i)
            return i.ToString(CultureInfo.InvariantCulture);
        if (tc.Value is bool b)
            return b ? "true" : "false";
        return tc.Value?.ToString() ?? "default";
    }

    /// <summary>
    /// Returns the type to use for distance calculation (Math.Abs cast target).
    /// </summary>
    private static string FormatDistanceType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Int32 => "int",
            SpecialType.System_Int64 => "long",
            _ => "double" // fallback: cast to double for distance comparison
        };
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
