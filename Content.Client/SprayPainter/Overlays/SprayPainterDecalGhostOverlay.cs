using Content.Client.Decals;
using Content.Client.Decals.Overlays;
using Content.Shared.Decals;
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.SprayPainter.Overlays;

/// <summary>
/// Overlay that shows a preview of the decal that will be painted when using the spray painter in decal add mode.
/// </summary>
public sealed class SprayPainterDecalGhostOverlay : DecalPlacementOverlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly EntityUid _sprayPainterUid;

    public SprayPainterDecalGhostOverlay(
        DecalPlacementSystem placement,
        SharedTransformSystem transform,
        SpriteSystem sprite,
        EntityUid sprayPainterUid) : base(placement, transform, sprite)
    {
        IoCManager.InjectDependencies(this);
        _sprayPainterUid = sprayPainterUid;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Only draw preview when in Add mode
        if (!_entityManager.TryGetComponent(_sprayPainterUid, out SprayPainterComponent? sprayPainterComp))
            return false;

        return sprayPainterComp.DecalMode == DecalPaintMode.Add;
    }

    protected override void LoadDecal()
    {
        if (!_entityManager.TryGetComponent(_sprayPainterUid, out SprayPainterComponent? sprayPainterComp))
        {
            decal = null;
            return;
        }

        // Load the decal prototype from the spray painter's selected decal
        if (!_protoManager.TryIndex<DecalPrototype>(sprayPainterComp.SelectedDecal, out var decalProto))
        {
            decal = null;
            return;
        }

        decal = decalProto;
        snap = sprayPainterComp.SnapDecals;
        rotation = Angle.FromDegrees(sprayPainterComp.SelectedDecalAngle);
        color = sprayPainterComp.SelectedDecalColor;
    }
}
