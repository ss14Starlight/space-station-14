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
    [Dependency] private static readonly IPrototypeManager _manager = default!;
    [Dependency] private static readonly IConfigurationManager _config = default!;

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

        var difference = Math.Round(CalculateHeightDifference(GetHeight(target), GetHeight(source)) + CalculateWeightDifference(GetWeight(target), GetWeight(source)), 3);
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

    public static float GetWeight(HumanoidAppearanceComponent component)
    {
        var weight = 0f;
        if (!_manager.TryIndex(component.Species, out var species)) return weight;
        weight = ((component.Width * component.Height) / species.StandardWeight) * species.StandardDensity;
        return weight;
    }

    public static float GetHeight(HumanoidAppearanceComponent component)
    {
        var height = 0f;
        if (!_manager.TryIndex(component.Species, out var species)) return height;
        height = species.StandardSize * component.Height;
        return height;
    }

    // TODO: make this cvars
    public static double CalculateHeightDifference(float source, float target) => Math.Round(target / source * 0.6);
    public static double CalculateWeightDifference(float source, float target) => Math.Round(target / source * 0.4);
}
