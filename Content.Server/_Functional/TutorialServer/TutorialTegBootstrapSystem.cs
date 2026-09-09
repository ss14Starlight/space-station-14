using System.Linq;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Power.Generation.Teg;
using Content.Shared.Tag;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Aligns tutorial TEG center + circulators into a valid adjacency/rotation layout and marks the
/// generator as producing so <see cref="TutorialStepComplete.TegProducingPower"/> can complete
/// after the curriculum interact gate. Full hot/cold pipe loops are too brittle for 7×7 chambers.
/// </summary>
public sealed partial class TutorialTegBootstrapSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> CirculatorTag = "TutorialTegCirculator";

    [Dependency] private PowerReceiverSystem _power = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tags = default!;

    public void TryConfigureOnGrid(EntityUid gridUid)
    {
        EntityUid? center = null;
        var circulators = new List<EntityUid>(2);

        var tegQuery = EntityQueryEnumerator<TegGeneratorComponent, TransformComponent>();
        while (tegQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid && xform.ParentUid != gridUid)
                continue;
            center = uid;
            break;
        }

        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (xform.GridUid != gridUid && xform.ParentUid != gridUid)
                continue;

            if (_tags.HasTag(uid, CirculatorTag))
                circulators.Add(uid);
        }

        if (center == null)
            return;

        if (TryComp<MapGridComponent>(gridUid, out var grid) && circulators.Count >= 2)
            LayoutCirculators(center.Value, circulators, gridUid, grid);

        _power.SetNeedsPower(center.Value, false);

        // Practice TEG is pre-arranged; ensure the power sensor can complete after interact.
        if (TryComp<TegGeneratorComponent>(center.Value, out var teg) && teg.LastGeneration <= 0f)
            teg.LastGeneration = 25000f;

        if (TryComp<PowerSupplierComponent>(center.Value, out var supplier) && supplier.MaxSupply <= 0f)
            supplier.MaxSupply = 25000f;
    }

    private void LayoutCirculators(
        EntityUid center,
        List<EntityUid> circulators,
        EntityUid gridUid,
        MapGridComponent grid)
    {
        EnsureAnchoredRotated(center, Direction.East);

        var centerTile = _map.TileIndicesFor(gridUid, grid, Transform(center).Coordinates);
        var circA = circulators[0];
        var circB = circulators.First(c => c != circA);

        SnapToTile(circA, gridUid, grid, centerTile.Offset(Direction.East));
        SnapToTile(circB, gridUid, grid, centerTile.Offset(Direction.West));
        EnsureAnchoredRotated(circA, Direction.South);
        EnsureAnchoredRotated(circB, Direction.North);
    }

    private void SnapToTile(EntityUid uid, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var coords = _map.ToCoordinates(gridUid, tile, grid);
        _transform.SetCoordinates(uid, coords);
    }

    private void EnsureAnchoredRotated(EntityUid uid, Direction dir)
    {
        var xform = Transform(uid);
        if (xform.Anchored)
            _transform.Unanchor(uid, xform);

        _transform.SetLocalRotation(uid, dir.ToAngle());
        _transform.AnchorEntity(uid);
    }
}
