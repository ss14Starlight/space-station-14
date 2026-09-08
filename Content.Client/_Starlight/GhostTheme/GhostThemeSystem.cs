using Content.Client._Starlight.Overlay.Trail;
using Content.Shared._Starlight;
using Content.Shared._Starlight.GhostTheme;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.GhostTheme;

public sealed partial class GhostThemeSystem : EntitySystem
{
    private const string DefaultGhostTheme = "None";
    private const string DefaultGhostLayer = "ghostVariant";

    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private StarlightEntitySystem _entities = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<GhostThemeComponent> ent, ref AppearanceChangeEvent args)
    {
        var spriteType = _entities.Entity<SpriteComponent>(ent.Owner);

        if (!_appearance.TryGetData<string>(ent.Owner, GhostThemeVisualLayers.Base, out var theme)
            || !_appearance.TryGetData<Color>(ent.Owner, GhostThemeVisualLayers.Color, out var color)
            || !_prototypeManager.TryIndex<GhostThemePrototype>(theme, out var ghostThemePrototype))
            return;

        var themeLayer = _sprite.LayerMapReserve(spriteType, GhostThemeVisualLayers.Base);
        var defaultLayer = _sprite.LayerMapReserve(spriteType, DefaultGhostLayer);
        var useDefaultSprite = theme == DefaultGhostTheme;
        var activeLayer = useDefaultSprite ? defaultLayer : themeLayer;

        // Cause-of-death sprites only replace the default ghost, never a custom theme.
        _sprite.LayerSetVisible(spriteType, defaultLayer, useDefaultSprite);
        _sprite.LayerSetVisible(spriteType, themeLayer, !useDefaultSprite);

        if (!useDefaultSprite)
            _sprite.LayerSetSprite(spriteType, themeLayer, ghostThemePrototype.SpriteSpecifier.Sprite);

        var spriteColor = color != Color.White
            ? color
            : ghostThemePrototype.SpriteSpecifier.SpriteColor;
        _sprite.LayerSetColor(spriteType, activeLayer, spriteColor);
        _sprite.LayerSetScale(spriteType, activeLayer, ghostThemePrototype.SpriteSpecifier.SpriteScale);
        _sprite.SetDrawDepth(spriteType, DrawDepth.Default + 11);
        spriteType.Comp?.LayerSetShader(activeLayer, "unshaded");

        if (spriteType.Comp == null)
            return;

        spriteType.Comp.NoRotation = ghostThemePrototype.SpriteSpecifier.SpriteRotation;
        spriteType.Comp.OverrideContainerOcclusion = true;

        // Apply trail effect
        if (ghostThemePrototype.Trail != null)
        {
            var trail = EnsureComp<TrailComponent>(ent.Owner);
            trail.TrailColor = ghostThemePrototype.Trail.Color;
            trail.FadeColor = ghostThemePrototype.Trail.FadeColor;
            trail.MaxPoints = ghostThemePrototype.Trail.MaxPoints;
            trail.LineWidth = ghostThemePrototype.Trail.LineWidth;
            trail.MinDistance = ghostThemePrototype.Trail.MinDistance;
            trail.DecayDelay = ghostThemePrototype.Trail.DecayDelay;
            trail.DecayInterval = ghostThemePrototype.Trail.DecayInterval;
            trail.Shader = ghostThemePrototype.Trail.Shader;
            trail.Mode = ghostThemePrototype.Trail.Mode;
            trail.SkipSamples = ghostThemePrototype.Trail.SkipSamples;
        }
        else
        {
            RemComp<TrailComponent>(ent.Owner);
        }
    }
}
