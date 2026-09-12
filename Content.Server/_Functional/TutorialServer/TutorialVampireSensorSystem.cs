using Content.Shared._Functional.TutorialServer;
using Content.Shared._Starlight.Antags.Vampires.Components;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Advances vampire tutorial sensors (fangs / blood / class).
/// </summary>
public sealed partial class TutorialVampireSensorSystem : EntitySystem
{
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TutorialParticipantComponent, VampireComponent>();
        while (query.MoveNext(out var uid, out var part, out var vamp))
        {
            if (!_tutorial.TryGetCurrentSubGoal(uid, part, out var sub))
                continue;

            switch (sub.Complete)
            {
                case TutorialStepComplete.VampireFangsExtended when vamp.FangsExtended:
                    _tutorial.AdvanceSubGoal(uid);
                    break;
                case TutorialStepComplete.VampireBloodAbove when vamp.TotalBlood >= Math.Max(1, sub.MinCount):
                    _tutorial.AdvanceSubGoal(uid);
                    break;
                case TutorialStepComplete.VampireClassChosen when vamp.ChosenClassId != null:
                    _tutorial.AdvanceSubGoal(uid);
                    break;
            }
        }
    }
}
