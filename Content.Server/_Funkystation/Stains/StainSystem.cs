using Content.Shared._Funkystation.Stains.Components;
using Content.Shared._Funkystation.Stains.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.Stains;

public sealed partial class StainSystem : SharedStainSystem
{
    [Dependency] private TagSystem _tag = null!;
    [Dependency] private FlammableStainsSystem _flammableStains = null!; // Starlight

    private static readonly ProtoId<TagPrototype> Tag = "DNASolutionScannable";

    protected override void OnStained(Entity<StainableComponent> ent, Entity<SolutionComponent> solution)
    {
        base.OnStained(ent, solution);

        _tag.AddTag(ent.Owner, Tag);
        _flammableStains.OnStained(ent.Owner, solution.Comp.Solution); // Starlight
    }
}
