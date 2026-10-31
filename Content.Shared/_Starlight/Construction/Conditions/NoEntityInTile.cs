using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Starlight.Construction.Conditions;

[DataDefinition]
public sealed partial class NoEntityInTile : IConstructionCondition
{
    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();

    [DataField]
    public LocId GuideString = "construction-step-condition-no-entity-in-tile";

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var whitelist = entMan.System<EntityWhitelistSystem>();

        if (entMan.System<SharedTransformSystem>().GetGrid(location) is not { } gridUid
            || !entMan.TryGetComponent<MapGridComponent>(gridUid, out var grid))
            return true;

        foreach (var entity in mapSystem.GetAnchoredEntities((gridUid, grid), mapSystem.LocalToTile(gridUid, grid, location)))
        {
            if (whitelist.IsWhitelistPass(Blacklist, entity))
                return false;
        }

        return true;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        var components = Blacklist.Components ?? [];
        var tags = Blacklist.Tags?.Select(t => t.Id).ToArray() ?? [];
        var sizes = Blacklist.Sizes?.Select(s => s.Id).ToArray() ?? [];
        var toolQualities = Blacklist.ToolQualities?.Select(q => q.Id).ToArray() ?? [];

        return new()
        {
            Localization = GuideString,
            Arguments =
            [
                ("components", string.Join(", ", components)),
                ("componentCount", components.Length),
                ("tags", string.Join(", ", tags)),
                ("tagCount", tags.Length),
                ("sizes", string.Join(", ", sizes)),
                ("sizeCount", sizes.Length),
                ("toolQualities", string.Join(", ", toolQualities)),
                ("toolQualityCount", toolQualities.Length),
                ("requireAll", Blacklist.RequireAll ? "true" : "false"),
            ],
        };
    }
}
