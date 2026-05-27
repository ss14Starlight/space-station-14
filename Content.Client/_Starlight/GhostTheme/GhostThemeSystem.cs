using Content.Client._Starlight.Trail;
using Content.Server.Administration.Systems;
using Content.Shared._Starlight.GhostTheme;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.GhostTheme;

public sealed class GhostThemeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly StarlightEntitySystem _entities = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<GhostThemeComponent> ent, ref AppearanceChangeEvent args)
    {
        var spriteType = _entities.Entity<SpriteComponent>(ent.Owner);

        if (!_appearance.TryGetData<string>(ent.Owner, GhostThemeVisualLayers.Base, out var Theme)
            || !_appearance.TryGetData<Color>(ent.Owner, GhostThemeVisualLayers.Color, out var Color)
            || !_prototypeManager.TryIndex<GhostThemePrototype>(Theme, out var ghostThemePrototype))
            return;

        var layer = _sprite.LayerMapReserve(spriteType, GhostThemeVisualLayers.Base);
        _sprite.LayerSetSprite(spriteType, layer, ghostThemePrototype.SpriteSpecifier.Sprite);
        _sprite.LayerSetColor(spriteType, layer, Color != Color.White ? Color : ghostThemePrototype.SpriteSpecifier.SpriteColor);
        _sprite.LayerSetScale(spriteType, layer, ghostThemePrototype.SpriteSpecifier.SpriteScale);
        _sprite.SetDrawDepth(spriteType, DrawDepth.Default + 11);
        spriteType.Comp?.LayerSetShader(layer, "unshaded");

        if (spriteType.Comp == null)
            return;

        spriteType.Comp.NoRotation = ghostThemePrototype.SpriteSpecifier.SpriteRotation;
        spriteType.Comp.OverrideContainerOcclusion = true;

        // Apply trail effect
        if (ghostThemePrototype.Trail != null)
        {
            var trail = EnsureComp<TrailComponent>(ent.Owner);
            trail.TrailColor = ghostThemePrototype.Trail.Color;
            trail.MaxPoints = ghostThemePrototype.Trail.MaxPoints;
            trail.LineWidth = ghostThemePrototype.Trail.LineWidth;
            trail.MinDistance = ghostThemePrototype.Trail.MinDistance;
            trail.DecayDelay = ghostThemePrototype.Trail.DecayDelay;
            trail.DecayInterval = ghostThemePrototype.Trail.DecayInterval;
            trail.Shader = ghostThemePrototype.Trail.Shader;
        }
        else
        {
            RemComp<TrailComponent>(ent.Owner);
        }
    }
}
