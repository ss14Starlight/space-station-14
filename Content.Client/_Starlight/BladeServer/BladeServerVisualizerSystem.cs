using Content.Client.Items.Systems;
using Content.Shared._Starlight.BladeServer;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client._Starlight.BladeServer;

/// <summary>
/// This is a <see cref="VisualizerSystem{T}"/> for entities with <see cref="BladeServerComponent"/>. It basically just
/// makes the stripe a color or invisible if a color isn't specified.
/// </summary>
public sealed partial class BladeServerVisualizerSystem : VisualizerSystem<BladeServerComponent>
{
    [Dependency] private readonly IReflectionManager _reflect = default!;

    private string? _stripeKeyCache;
    private string _stripeKey => _stripeKeyCache ??= _reflect.GetEnumReference(BladeServerVisuals.StripeLayer);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BladeServerComponent, GetInhandVisualsEvent>(OnGetInhandVisuals,
            after: [typeof(ItemSystem)]);
    }

    protected override void OnAppearanceChange(
        EntityUid uid,
        BladeServerComponent component,
        ref AppearanceChangeEvent args
    )
    {
        if (args.Sprite is not { } sprite)
            return;

        var entity = new Entity<SpriteComponent?, AppearanceComponent>(uid, sprite, args.Component);
        if (AppearanceSystem.TryGetData<Color>(entity, BladeServerVisuals.StripeColor, out var stripeColor, entity))
        {
            SpriteSystem.LayerSetColor(entity, BladeServerVisuals.StripeLayer, stripeColor);
            SpriteSystem.LayerSetVisible(entity, BladeServerVisuals.StripeLayer, true);
        }
        else
        {
            SpriteSystem.LayerSetVisible(entity, BladeServerVisuals.StripeLayer, false);
        }
    }

    private void OnGetInhandVisuals(Entity<BladeServerComponent> entity, ref GetInhandVisualsEvent args)
    {
        // If there's no stripe color, remove the stripe layer from the visuals entirely
        // to prevent HandsSystem from trying to add/remove a layer that shouldn't exist
        if (entity.Comp.StripeColor == null)
        {
            args.Layers.RemoveAll(x => x.Item1 == _stripeKey);
            return;
        }

        // Otherwise, set the stripe layer to be visible with the appropriate color
        foreach (var (key, layer) in args.Layers)
        {
            if (key != _stripeKey)
                continue;

            layer.Visible = true;
            layer.Color = entity.Comp.StripeColor;
        }
    }
}