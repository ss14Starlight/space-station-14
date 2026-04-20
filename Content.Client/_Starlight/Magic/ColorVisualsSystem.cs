using Content.Client.Items.Systems;
using Content.Shared._Starlight.Magic.Systems;
using Content.Shared.Hands;
using Robust.Client.GameObjects;

namespace Content.Client._Starlight.Magic;

public sealed class ColorVisualsSystem : VisualizerSystem<ColorVisualsComponent>
{
    [Dependency] private readonly ItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ColorVisualsComponent, GetInhandVisualsEvent>(OnGetVisuals, after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(EntityUid uid, ColorVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite) || !AppearanceSystem.TryGetData<Color>(uid, ColorVisuals.Color, out var color, args.Component))
            return;

        sprite[ColorVisuals.Color].Color = color;

        _item.VisualsChanged(uid);
    }

    private void OnGetVisuals(EntityUid uid, ColorVisualsComponent item, GetInhandVisualsEvent args)
    {
        if (!AppearanceSystem.TryGetData<Color>(uid, ColorVisuals.Color, out var color))
            return;

        foreach (var layer in args.Layers)
            layer.Item2.Color = color;
    }
}
