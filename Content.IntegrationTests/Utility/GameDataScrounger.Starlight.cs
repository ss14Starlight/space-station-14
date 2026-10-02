
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Utility;

public static partial class GameDataScrounger
{
    private static readonly YamlScalarNode _valuesNode = new("values");
    private static IEnumerable<string> GetPrototypeIds(YamlNode id)
    {
        if (id is YamlScalarNode { Value: { } scalarId })
        {
            yield return scalarId;
            yield break;
        }

        if (!(id is YamlMappingNode variants && variants.Tag == $"type:{nameof(CreateVariants)}" &&
            variants[_valuesNode] is YamlSequenceNode values))
            yield break;

        foreach (var value in values.Children)
        {
            if (value is YamlScalarNode { Value: { } variantId })
                yield return variantId;
        }
    }
}
