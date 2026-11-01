using Content.Client.Chemistry.Visualizers;
using Content.Shared._Starlight.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Item;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Chemistry.Visualizers;

public sealed partial class ChameleonGlassHeldVisualsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [SubscribeLocalEvent(after: [typeof(SolutionContainerVisualsSystem)])]
    private void OnGetHeldVisuals(Entity<ChameleonGlassComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!TryComp<SolutionContainerVisualsComponent>(ent, out var visuals) ||
            !visuals.ChangeColor ||
            visuals.InHandsFillBaseName is not { } fillBaseName ||
            !TryComp<ItemComponent>(ent, out var item) ||
            !_appearance.TryGetData<string>(ent, ChameleonGlassVisuals.Override, out var disguise) ||
            string.IsNullOrEmpty(disguise) ||
            !_prototypes.TryIndex<ReagentPrototype>(disguise, out var reagent))
            return;

        var heldPrefix = item.HeldPrefix == null ? "inhand-" : $"{item.HeldPrefix}-inhand-";
        var location = args.Location switch
        {
            HandLocation.Left => "left",
            HandLocation.Right => "right",
            _ => "left"
        };
        var fillPrefix = $"{heldPrefix}{location}{fillBaseName}";

        foreach (var (key, layer) in args.Layers)
        {
            if (key.StartsWith(fillPrefix, StringComparison.Ordinal))
                layer.Color = reagent.SubstanceColor;
        }
    }
}
