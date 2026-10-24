using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Objectives.ObjectivePicker;

public sealed partial class PotentialObjectivesSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AntagRandomObjectivesSystem _antagObjectives = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PotentialObjectivesComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.AutoSelectionTime || comp.ObjectiveOptions.Count == 0)
                continue;

            // Timeout must obey the same difficulty and compatibility rules as a player.
            _antagObjectives.AutoSelectObjectives(uid, comp);
        }
    }
}
