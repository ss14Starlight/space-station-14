using Content.Client._Starlight.Medical.Body.Systems;
using Content.Client.Alerts;
using Content.Shared._Starlight.Medical.Limbs;
using Content.Shared.Body.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Medical.Limbs;
public sealed class LimbSystem : SharedLimbSystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
    }

    private enum LimbAlertVisualLayers : byte
    {
        ArmRight,
        ArmLeft,
        LegRight,
        LegLeft
    }

    private void OnUpdateAlert(EntityUid uid, BodyComponent component, UpdateAlertSpriteEvent args)
    {
        var key = args.Alert.AlertKey.AlertType;
        if (key != "LostLimb")
            return;

        _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.ArmRight, false);
        _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.ArmLeft, false);
        _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.LegRight, false);
        _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.LegLeft, false);

        var (root, comp) = _body.GetRootPartOrNull(uid)!.Value;
        foreach (var slot in _body.TryGetFreePartSlots(root, null))
        {
            if (slot is "right arm")
                _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.ArmRight, true);
            if (slot is "left arm")
                _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.ArmLeft, true);
            if (slot is "right leg")
                _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.LegRight, true);
            if (slot is "left leg")
                _sprite.LayerSetVisible((args.SpriteViewEnt, args.SpriteViewEnt.Comp), LimbAlertVisualLayers.LegLeft, true);
        }

    }
}
