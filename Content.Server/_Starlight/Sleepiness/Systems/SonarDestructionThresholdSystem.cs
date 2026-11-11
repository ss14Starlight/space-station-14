using Content.Server.Destructible;
using Content.Shared._Starlight.Sleepiness.Components;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;

namespace Content.Server._Starlight.Sleepiness.Systems;

[UsedImplicitly]
public sealed partial class SonarDestructionThresholdSystem : EntitySystem
{
    [Dependency] private DestructibleSystem _destructible = default!;

    [SubscribeLocalEvent]
    private void OnDestructibleStartup(Entity<DestructibleComponent> ent, ref ComponentStartup args)
    {
        var destructionThreshold = _destructible.DestroyedAt(ent.Owner, ent.Comp);
        if (destructionThreshold <= FixedPoint2.Zero || destructionThreshold == FixedPoint2.MaxValue)
            return;

        var sonarThreshold = EnsureComp<SonarDamageThresholdComponent>(ent.Owner);
        if (sonarThreshold.DestructionThreshold == destructionThreshold)
            return;

        sonarThreshold.DestructionThreshold = destructionThreshold;
        Dirty(ent.Owner, sonarThreshold);
    }
}
