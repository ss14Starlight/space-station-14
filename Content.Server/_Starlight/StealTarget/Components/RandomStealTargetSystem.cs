using Robust.Shared.Random;

namespace Content.Server._Starlight.StealTarget.Components;

public sealed class RandomStealTargetSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RandomStealTargetComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, RandomStealTargetComponent component, ComponentInit args)
    {
        EnsureComp<StealTargetComponent>(uid, out var stealTarget);

        stealTarget.StealGroup = _random.Pick(component.StealTargetNames);
    }

}
