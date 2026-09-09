using Content.Client.Parallax;
using Content.Client.Weather;
using Content.Client._Starlight.Sprite;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private ParallaxSystem _parallax = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteQualitySystem _spriteQuality = default!; // Starlight
    [Dependency] private WeatherSystem _weather = default!;
    [Dependency] private StatusEffectsSystem _status = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new StencilOverlay(_parallax, _transform, _map, _sprite, _spriteQuality, _weather, _status)); // Starlight
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<StencilOverlay>();
    }
}
