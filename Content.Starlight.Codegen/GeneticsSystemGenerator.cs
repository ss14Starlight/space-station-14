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
    private const string EnumBaseAttrFullName = "Content.Shared.Genetics.GeneticsEnumBasedVariableAttribute";
    private const string EnumEntryAttrFullName = "Content.Shared.Genetics.GeneticsEnumEntryAttribute";
    private const string VariableAttrNamespace = "Content.Shared.Genetics";

    private static readonly (string Namespace, string ClassName, string Modifiers)[] Targets =
    {
        ("Content.Shared.Genetics", "SharedGeneticsSystem", "public abstract partial class"),
        ("Content.Server.Genetics", "GeneticsSystem",       "public sealed partial class"),
    };

    // ── Data models (no records — netstandard2.0 compat) ──

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

    private sealed class EnumEntryInfo
    {
        public readonly int Complexity;
        public readonly int Stability;
        public readonly string Key;

        public EnumEntryInfo(int complexity, int stability, string key)
        {
            Complexity = complexity;
            Stability = stability;
            Key = key;
        }
    }

    private sealed class EnumVariableInfo
    {
        public readonly string MemberName;
        public readonly string GetterMethod;
        public readonly string SetterMethod;
        public readonly List<EnumEntryInfo> Entries;
        public readonly int RegionLength;
        public readonly int OffsetInVariableRegion;

        public EnumVariableInfo(string memberName, string getterMethod, string setterMethod, List<EnumEntryInfo> entries, int regionLength, int offsetInVariableRegion)
        {
            MemberName = memberName;
            GetterMethod = getterMethod;
            SetterMethod = setterMethod;
            Entries = entries;
            RegionLength = regionLength;
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

        // Collect per-component info
        var componentMultiValueVars = new Dictionary<string, List<VariableInfo>>();
        var componentEnumVars = new Dictionary<string, List<EnumVariableInfo>>();

        foreach (var componentDef in allComponents)
        {
            var multiVars = GetMultiValueVariables(componentDef.Class);
            var enumVars = GetEnumVariables(componentDef.Class, multiVars.Sum(v => v.CodonCount));
            componentMultiValueVars[componentDef.Class.Name] = multiVars;
            componentEnumVars[componentDef.Class.Name] = enumVars;
        }

        // ── InitializeGenerated ──
        sb.AppendLine("    private void InitializeGenerated()");
        sb.AppendLine("    {");
        sb.AppendLine("");

        foreach (var componentDef in allComponents)
        {
            sb.AppendLine($"        SubscribeLocalEvent<{componentDef.Class.Name}, ComponentInit>(OnComponentInit{componentDef.Class.Name});");
        }
        sb.AppendLine("");

        foreach (var componentDef in allComponents)
        {
            var complexity = GetAttributeArg(componentDef.Attribute, 0, 2);
            var stability = GetAttributeArg(componentDef.Attribute, 1, 1);

            var multiVars = componentMultiValueVars[componentDef.Class.Name];
            var enumVars = componentEnumVars[componentDef.Class.Name];
            var varCodonCount = multiVars.Sum(v => v.CodonCount) + enumVars.Sum(v => v.RegionLength);
            var hasAnyVariables = multiVars.Count > 0 || enumVars.Count > 0;

            sb.AppendLine($"        GeneticComponents[typeof({componentDef.Class.Name})] = new GeneticComponentInfo(typeof({componentDef.Class.Name}), {complexity}, {stability}, {varCodonCount});");

            if (hasAnyVariables)
            {
                sb.AppendLine($"        VariableSyncWriteDna[typeof({componentDef.Class.Name})] = SyncWrite{componentDef.Class.Name}Variables;");
                sb.AppendLine($"        VariableSyncReadDna[typeof({componentDef.Class.Name})] = SyncRead{componentDef.Class.Name}Variables;");
            }

            // Register enum variables
            foreach (var ev in enumVars)
            {
                sb.AppendLine($"        EnumVariables[(typeof({componentDef.Class.Name}), \"{ev.MemberName}\")] = new EnumVariableInfo");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            GetterMethod = \"{ev.GetterMethod}\",");
                sb.AppendLine($"            SetterMethod = \"{ev.SetterMethod}\",");
                sb.AppendLine($"            RegionLength = {ev.RegionLength},");
                sb.AppendLine($"            OffsetInVariableRegion = {ev.OffsetInVariableRegion},");
                sb.AppendLine($"            Entries = new System.Collections.Generic.List<EnumEntryDefinition>");
                sb.AppendLine($"            {{");
                foreach (var entry in ev.Entries)
                {
                    sb.AppendLine($"                new EnumEntryDefinition({entry.Complexity}, {entry.Stability}, \"{EscapeString(entry.Key)}\"),");
                }
                sb.AppendLine($"            }},");
                sb.AppendLine($"        }};");
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
            var multiVars = componentMultiValueVars[componentDef.Class.Name];
            var enumVars = componentEnumVars[componentDef.Class.Name];
            if (multiVars.Count == 0 && enumVars.Count == 0)
                continue;

            var className = componentDef.Class.Name;

            // SyncWrite: value → DNA
            sb.AppendLine($"    private void SyncWrite{className}Variables(EntityUid uid, char[] chars, RoundGeneticRecord record)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (!EntityManager.TryGetComponent<{className}>(uid, out var comp))");
            sb.AppendLine("            return;");

            foreach (var v in multiVars)
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

            foreach (var ev in enumVars)
            {
                sb.AppendLine($"        if (EnumVariables.TryGetValue((typeof({className}), \"{ev.MemberName}\"), out var enumInfo_{ev.MemberName}))");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var key = comp.{ev.GetterMethod}();");
                sb.AppendLine($"            var entryIndex = -1;");
                sb.AppendLine($"            if (key != null)");
                sb.AppendLine($"            {{");
                sb.AppendLine($"                for (var i = 0; i < enumInfo_{ev.MemberName}.Entries.Count; i++)");
                sb.AppendLine($"                {{");
                sb.AppendLine($"                    if (enumInfo_{ev.MemberName}.Entries[i].Key == key)");
                sb.AppendLine($"                    {{");
                sb.AppendLine($"                        entryIndex = i;");
                sb.AppendLine($"                        break;");
                sb.AppendLine($"                    }}");
                sb.AppendLine($"                }}");
                sb.AppendLine($"            }}");
                sb.AppendLine($"            if (entryIndex >= 0)");
                sb.AppendLine($"                WriteEnumCanonical(chars, record, enumInfo_{ev.MemberName}, entryIndex);");
                sb.AppendLine($"            else");
                sb.AppendLine($"                ScrambleEnumRegion(chars, record, enumInfo_{ev.MemberName});");
                sb.AppendLine($"        }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("");

            // SyncRead: DNA → value
            sb.AppendLine($"    private void SyncRead{className}Variables(EntityUid uid, string dna, RoundGeneticRecord record)");
            sb.AppendLine("    {");
            sb.AppendLine($"        if (!EntityManager.TryGetComponent<{className}>(uid, out var comp))");
            sb.AppendLine("            return;");

            foreach (var v in multiVars)
            {
                var typeName = v.FieldType.ToDisplayString();
                var valuesLiteral = FormatValuesArray(v.FieldType, v.Values);

                sb.AppendLine($"        {{");
                sb.AppendLine($"            var matches = CountVariableMatches(dna.AsSpan(), record, {v.OffsetInVariableRegion}, {v.CodonCount});");
                sb.AppendLine($"            var values = new {typeName}[] {{ {valuesLiteral} }};");
                sb.AppendLine($"            comp.{v.MemberName} = values[Math.Min(matches, values.Length - 1)];");
                sb.AppendLine($"        }}");
            }

            foreach (var ev in enumVars)
            {
                sb.AppendLine($"        if (EnumVariables.TryGetValue((typeof({className}), \"{ev.MemberName}\"), out var enumInfo_{ev.MemberName}))");
                sb.AppendLine($"        {{");
                sb.AppendLine($"            var bestEntry = FindBestEnumMatch(dna.AsSpan(), record, enumInfo_{ev.MemberName});");
                sb.AppendLine($"            if (bestEntry >= 0)");
                sb.AppendLine($"                comp.{ev.SetterMethod}(enumInfo_{ev.MemberName}.Entries[bestEntry].Key);");
                sb.AppendLine($"            else");
                sb.AppendLine($"                comp.{ev.SetterMethod}(null);");
                sb.AppendLine($"        }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ── Member scanning ──

    /// <summary>
    /// Scan members for [GeneticMultiValueVariable] attributes.
    /// </summary>
    private static List<VariableInfo> GetMultiValueVariables(INamedTypeSymbol classSymbol)
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
    /// Scan members for [GeneticsEnumBasedVariable] + [GeneticsEnumEntry] attribute pairs.
    /// </summary>
    private static List<EnumVariableInfo> GetEnumVariables(INamedTypeSymbol classSymbol, int startingOffset)
    {
        var result = new List<EnumVariableInfo>();
        var currentOffset = startingOffset;

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is not (IFieldSymbol or IPropertySymbol))
                continue;

            // Look for the marker attribute
            string? getterMethod = null;
            string? setterMethod = null;
            var entries = new List<EnumEntryInfo>();

            foreach (var attr in member.GetAttributes())
            {
                var fullName = attr.AttributeClass?.ToDisplayString();
                if (fullName == EnumBaseAttrFullName)
                {
                    getterMethod = attr.ConstructorArguments[0].Value as string;
                    setterMethod = attr.ConstructorArguments[1].Value as string;
                }
                else if (fullName == EnumEntryAttrFullName
                         && attr.ConstructorArguments[0].Value is int complexity
                         && attr.ConstructorArguments[1].Value is int stability
                         && attr.ConstructorArguments[2].Value is string key)
                {
                    entries.Add(new EnumEntryInfo(complexity, stability, key));
                }
            }

            if (getterMethod != null && setterMethod != null && entries.Count > 0)
            {
                var regionLength = entries.Max(e => e.Complexity + e.Stability);

                result.Add(new EnumVariableInfo(
                    member.Name,
                    getterMethod,
                    setterMethod,
                    entries,
                    regionLength,
                    currentOffset));

                currentOffset += regionLength;
            }
        }

        return result;
    }

    // ── Formatting helpers ──

    private static string FormatValuesArray(ITypeSymbol type, ImmutableArray<TypedConstant> values)
    {
        return string.Join(", ", values.Select(v => FormatLiteral(type, v)));
    }

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

    private static string FormatDistanceType(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Int32 => "int",
            SpecialType.System_Int64 => "long",
            _ => "double"
        };
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

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
