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
public sealed class SprayPainterDecalGhostOverlay : DecalPlacementOverlay // Inherit from the decal placement overlay since the functionality is very similar, just with a different way of loading the decal prototype and only showing in certain modes
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private readonly EntityUid _sprayPainterUid;

    public SprayPainterDecalGhostOverlay( // Pass dependencies to the base decal placement overlay
        DecalPlacementSystem placement,
        SharedTransformSystem transform,
        SpriteSystem sprite,
        EntityUid sprayPainterUid) : base(placement, transform, sprite) 
    {
        IoCManager.InjectDependencies(this); 
        _sprayPainterUid = sprayPainterUid;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) // Only draw the decal ghost when the spray painter is in decal add mode
    {

        if (!_entityManager.TryGetComponent(_sprayPainterUid, out SprayPainterComponent? sprayPainterComp))
            return false;

        return sprayPainterComp.DecalMode == DecalPaintMode.Add;
    }

    protected override void LoadDecal() // Load the decal prototype based on the selected decal in the spray painter component, and load the snap and rotation settings from the component as well
    {
        if (!_entityManager.TryGetComponent(_sprayPainterUid, out SprayPainterComponent? sprayPainterComp)) // If we can't find the spray painter component, don't draw anything
        {
            decal = null;
            return;
        }

        if (!_protoManager.TryIndex<DecalPrototype>(sprayPainterComp.SelectedDecal, out var decalProto)) // If we can't find the decal prototype, don't draw anything
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
