using Content.Shared._Afterlight.Silicons.Borgs;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// Client side logic for borg type switching. Sets up primarily client-side visual information.
/// </summary>
/// <seealso cref="SharedBorgSwitchableTypeSystem"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
public sealed partial class BorgSwitchableTypeSystem : SharedBorgSwitchableTypeSystem
{
    [Dependency] private BorgSystem _borgSystem = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, AfterAutoHandleStateEvent>(AfterStateHandler);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<BorgSwitchableTypeComponent> ent, ref ComponentStartup args)
    {
        UpdateEntityAppearance(ent);
    }

    private void AfterStateHandler(Entity<BorgSwitchableTypeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateEntityAppearance(ent);
        _borgSystem.UpdateUI((ent.Owner, null)); // Starlight: refresh the reset-chassis button instead of polling every frame
    }

    protected override void UpdateEntityAppearance(
        Entity<BorgSwitchableTypeComponent> entity,
        BorgTypePrototype prototype)
    {
        // AL - added checks to stop sprite state errors
        if (!TryComp<BorgSwitchableSubtypeComponent>(entity, out var subtype) ||
            subtype.BorgSubtype != null)
            return;

        if (TryComp(entity, out SpriteComponent? sprite))
        {
            if (_resourceCache.TryGetResource<RSIResource>(
                    SpriteSpecifierSerializer.TextureRoot / prototype.SpritePath,
                    out var res))
            {
                sprite.BaseRSI = res.RSI;
                _sprite.LayerSetRsi((entity.Owner, sprite), BorgVisualLayers.Body, rsi: null); // Starlight
                _sprite.LayerSetRsi((entity.Owner, sprite), BorgVisualLayers.Light, rsi: null); // Starlight
                _sprite.LayerSetRsi((entity.Owner, sprite), BorgVisualLayers.LightStatus, rsi: null); // Starlight
            }
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.Body, prototype.SpriteBodyState);
            _sprite.LayerSetRsiState((entity, sprite), BorgVisualLayers.LightStatus, prototype.SpriteToggleLightState);
        }

        if (TryComp(entity, out BorgChassisComponent? chassis))
        {
            _borgSystem.SetMindStates(
                (entity.Owner, chassis),
                prototype.SpriteHasMindState,
                prototype.SpriteNoMindState);

            if (TryComp(entity, out AppearanceComponent? appearance))
            {
                // Queue update so state changes apply.
                _appearance.QueueUpdate(entity, appearance);
            }
        }

        base.UpdateEntityAppearance(entity, prototype);
    }
}
