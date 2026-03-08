using Content.Server._Starlight.Plumbing.Components;
using Content.Shared._Starlight.Plumbing;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using JetBrains.Annotations;
using SharedAppearanceSystem = Robust.Shared.GameObjects.SharedAppearanceSystem;

namespace Content.Server._Starlight.Plumbing.EntitySystems;

/// <summary>
/// Keeps plumbing disposal running visuals in sync with whether there are reagents in its drain buffer.
/// Reagent destruction is handled by the shared <see cref="Content.Shared.Fluids.EntitySystems.DrainSystem"/>.
/// </summary>
[UsedImplicitly]
public sealed class PlumbingDisposalSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DrainComponent, PlumbingDeviceUpdateEvent>(OnDeviceUpdate);
    }

    private void OnDeviceUpdate(Entity<DrainComponent> ent, ref PlumbingDeviceUpdateEvent args)
    {
        if (!_solutionSystem.ResolveSolution(ent.Owner, DrainComponent.SolutionName, ref ent.Comp.Solution, out var buffer))
            return;

        _appearance.SetData(ent.Owner, PlumbingVisuals.Running, buffer.Volume > 0);
    }
}