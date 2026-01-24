using Content.Shared._Starlight.Legendary;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Starlight.Legendary;

public sealed class LegendarySpriteSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegendarySpriteComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, LegendarySpriteComponent component, ComponentStartup args)
        => ApplyLegendarySprite(uid, component);

    private void ApplyLegendarySprite(EntityUid uid, LegendarySpriteComponent component)
    {
        if (component.RsiPath == default)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateSpriteRsi(uid, sprite, component.RsiPath);
    }

    private void UpdateSpriteRsi(EntityUid uid, SpriteComponent sprite, ResPath rsiPath)
    {
        var fullPath = new ResPath("/Textures") / rsiPath;

        var layerCount = 0;
        foreach (var _ in sprite.AllLayers)
            layerCount++;

        for (var i = 0; i < layerCount; i++)
        {
            _sprite.LayerSetRsi((uid, sprite), i, fullPath);
        }
    }
}
