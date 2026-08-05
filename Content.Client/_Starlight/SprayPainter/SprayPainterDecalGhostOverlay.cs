using Content.Client.Decals;
using Content.Client.Decals.Overlays;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;

namespace Content.Client._Starlight.SprayPainter;

public sealed partial class SprayPainterDecalGhostOverlay : DecalPlacementOverlay
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    private SharedInteractionSystem _interaction;
    private readonly DecalPrototype? _decalPrototype;
    private readonly Angle _rotation;
    private readonly Color? _color;
    private readonly bool _gridSnap;

    public SprayPainterDecalGhostOverlay(DecalPlacementSystem placement, SharedTransformSystem transform, SpriteSystem sprite, SharedInteractionSystem interaction, DecalPrototype? decalPrototype, Angle rotation, Color? color, bool gridSnap) : base(placement, transform, sprite)
    {
        IoCManager.InjectDependencies(this);
        _interaction = interaction;
        _decalPrototype = decalPrototype;
        _rotation = float.DegreesToRadians((float)rotation);
        _color = color;
        _gridSnap = gridSnap;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var mouseScreenPos = _inputManager.MouseScreenPosition;
        var mousePos = _eyeManager.PixelToMap(mouseScreenPos);

        var player = _playerManager.LocalEntity;
        if (player is null)
            return false;
        return _interaction.InRangeUnobstructed(player.Value, mousePos);
    }

    protected override void LoadDecal()
    {
        decal = _decalPrototype;
        snap = _gridSnap;
        rotation = _rotation;
        color = _color;
    }
}
