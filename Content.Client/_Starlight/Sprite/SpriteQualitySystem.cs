using Content.Shared._Starlight.CCVar;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Sprite;

/// <summary>
/// Selects and applies sprite variants using the client's sprite-quality setting.
/// </summary>
public sealed partial class SpriteQualitySystem : EntitySystem
{
    private static readonly ResPath TextureRoot = new("/Textures");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private SpriteQualityLevel _quality = SpriteQualityLevel.High;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpriteQualityComponent, ComponentStartup>(OnComponentStartup);
        Subs.CVar(_cfg, StarlightCCVars.SpriteQuality, OnQualityChanged, true);
    }

    private void OnComponentStartup(Entity<SpriteQualityComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            ApplySprite((ent, ent.Comp, sprite));
    }

    private void OnQualityChanged(int value)
    {
        _quality = value switch
        {
            (int) SpriteQualityLevel.Low => SpriteQualityLevel.Low,
            (int) SpriteQualityLevel.Medium => SpriteQualityLevel.Medium,
            _ => SpriteQualityLevel.High,
        };

        var query = EntityQueryEnumerator<SpriteQualityComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var quality, out var sprite))
        {
            ApplySprite((uid, quality, sprite));
        }
    }

    private void ApplySprite(Entity<SpriteQualityComponent, SpriteComponent> ent)
    {
        var variant = GetVariant(ent.Comp1);
        if (variant == null)
            return;

        LoadVariant(variant);

        ApplyLayer((ent.Owner, ent.Comp2), ent.Comp1.Layer, variant.BaseLayer);

        foreach (var (index, data) in variant.Layers)
        {
            ApplyLayer((ent.Owner, ent.Comp2), index, data);
        }
    }

    /// <summary>
    /// Gets the active quality variant from an entity, or <paramref name="fallback"/> if it has none.
    /// </summary>
    public SpriteSpecifier? GetSprite(EntityUid uid, SpriteSpecifier? fallback = null)
    {
        if (!TryComp<SpriteQualityComponent>(uid, out var component) ||
            GetVariant(component) is not { } variant)
        {
            return fallback;
        }

        return GetSpecifier(variant.BaseLayer) ?? fallback;
    }

    private SpriteQualityComponent.Variant? GetVariant(SpriteQualityComponent component)
    {
        if (component.Variants.TryGetValue(_quality, out var variant))
            return variant;

        // Should always render *something*
        if (component.Variants.TryGetValue(SpriteQualityLevel.High, out variant))
            return variant;

        if (component.Variants.TryGetValue(SpriteQualityLevel.Medium, out variant))
            return variant;

        return component.Variants.TryGetValue(SpriteQualityLevel.Low, out variant)
            ? variant
            : null;
    }

    private void ApplyLayer(Entity<SpriteComponent> ent, int index, PrototypeLayerData data)
    {
        if (!_sprite.TryGetLayer((ent.Owner, ent.Comp), index, out var layer, false))
            return;

        _sprite.LayerSetData(layer, data);

        // Keep animated replacements aligned to the same clock instead of restarting them.
        _sprite.SetAutoAnimateSync(ent.Comp, layer, _timing.RealTime.TotalSeconds);
    }

    private void LoadVariant(SpriteQualityComponent.Variant variant)
    {
        LoadLayer(variant.BaseLayer);

        foreach (var layer in variant.Layers.Values)
        {
            LoadLayer(layer);
        }
    }

    private void LoadLayer(PrototypeLayerData data)
    {
        if (GetSpecifier(data) is { } specifier)
            _sprite.GetFrame(specifier, TimeSpan.Zero);
    }

    private static SpriteSpecifier? GetSpecifier(PrototypeLayerData data)
    {
        var rsiPath = data.RsiPath;
        var state = data.State;
        if (!string.IsNullOrWhiteSpace(rsiPath) &&
            !string.IsNullOrWhiteSpace(state))
        {
            return new SpriteSpecifier.Rsi(TextureRoot / rsiPath, state);
        }

        var texturePath = data.TexturePath;
        return !string.IsNullOrWhiteSpace(texturePath)
            ? new SpriteSpecifier.Texture(TextureRoot / texturePath)
            : null;
    }
}
