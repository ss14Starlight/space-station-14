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
        return new ConstructionGuideEntry
        {
            Localization = GuideString
        };
    }
}
