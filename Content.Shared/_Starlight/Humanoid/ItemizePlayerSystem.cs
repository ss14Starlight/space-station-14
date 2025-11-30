using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Item;
using Content.Shared.Starlight.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Humanoid;

public sealed class ItemizePlayerSystem : EntitySystem // uppi
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidAppearanceComponent, ItemBeingPickedUpEvent>(AllowPickup);
    }

    private void AllowPickup(Entity<HumanoidAppearanceComponent> ent, ref ItemBeingPickedUpEvent args)
    {

        if (!TryComp<HumanoidAppearanceComponent>(args.User, out var source) || !TryComp<HumanoidAppearanceComponent>(args.Item, out var target))
        {
            return;
        }


        var targetHeight = 0f;
        var targetWeight = 0f;
        var sourceHeight = 0f;
        var sourceWeight = 0f;

        if (_prototypeManager.TryIndex(target.Species, out var targetSpecies))
        {
            targetHeight = targetSpecies.StandardSize * target.Height;
            targetWeight = (targetSpecies.StandardWeight + targetSpecies.StandardDensity) * ((target.Width * target.Height) - 1);

        }

        if (_prototypeManager.TryIndex(source.Species, out var sourceSpecies))
        {
            sourceHeight = sourceSpecies.StandardSize * source.Height;
            sourceWeight = (sourceSpecies.StandardWeight + sourceSpecies.StandardDensity) * ((source.Width * source.Height) - 1);
        }

        var difference = Math.Round(targetHeight / sourceHeight * 0.6 + targetWeight / sourceWeight * 0.4, 3);
        if (difference >= _config.GetCVar(StarlightCCVars.MaxPickupDifference) && !HasComp<SmallSpeciesComponent>(args.Item))
        {
            args.Cancelled = true;
            return;
        }


        var scale = difference / 0.75;
        var width = (int)Math.Ceiling(6 * scale);
        var height = (int)Math.Ceiling(4 * scale);

        TryComp<ItemComponent>(args.Item, out var item);
        _item.SetShape(args.Item, new List<Box2i> { Box2i.FromDimensions(0, 0, height, width) }, item);
    }
}
