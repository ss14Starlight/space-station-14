using Content.Client.Alerts;
using Content.Shared._Starlight.Antags.Vampires;
using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Antags.Vampires;

public sealed class VampireSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<FactionIconPrototype> _thrallIcon = "VampireThrallIcon";
    private static readonly ProtoId<FactionIconPrototype> _masterIcon = "VampireFaction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VampireComponent, UpdateAlertSpriteEvent>(OnUpdateAlert);
        SubscribeLocalEvent<VampireThrallComponent, GetStatusIconsEvent>(OnThrallIcons);
        SubscribeLocalEvent<VampireComponent, GetStatusIconsEvent>(OnVampireIcons);
    }

    private void OnUpdateAlert(EntityUid uid, VampireComponent comp, ref UpdateAlertSpriteEvent args)
    {
        var key = args.Alert.AlertKey.AlertType;

        if (key == "VampireBlood")
        {
            // Background is set by the alert -> only set the digit layers from the counter value.
            var value = Math.Clamp(comp.DrunkBlood, 0, 9999);
            var d1 = value / 1000 % 10;
            var d2 = value / 100 % 10;
            var d3 = value / 10 % 10;
            var d4 = value % 10;

            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit1, d1.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit2, d2.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit3, d3.ToString());
            _sprite.LayerSetRsiState((args.SpriteViewEnt, args.SpriteViewEnt.Comp), VampireVisualLayers.Digit4, d4.ToString());
        }
    }

    private void OnThrallIcons(EntityUid uid, VampireThrallComponent component, ref GetStatusIconsEvent ev)
    {
        if (_prototype.TryIndex(_thrallIcon, out var icon))
            ev.StatusIcons.Add(icon);
    }

    private void OnVampireIcons(EntityUid uid, VampireComponent component, ref GetStatusIconsEvent ev)
    {
        if (_prototype.TryIndex(_masterIcon, out var icon))
            ev.StatusIcons.Add(icon);
    }
}

