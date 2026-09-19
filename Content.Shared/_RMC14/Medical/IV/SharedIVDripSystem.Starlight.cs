using Content.Shared.Body.Components;
using Content.Shared.Mobs.Components;

namespace Content.Shared._RMC14.Medical.IV;

public abstract partial class SharedIVDripSystem : EntitySystem
{
    private bool CanAttach(EntityUid target) => HasComp<MobStateComponent>(target) &&
            TryComp(target, out BloodstreamComponent? bloodstream) &&
            _solutionContainer.TryGetSolution(target, bloodstream.BloodSolutionName, out _);
}
