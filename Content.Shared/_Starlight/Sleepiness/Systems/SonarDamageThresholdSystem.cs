using Content.Shared._Starlight.Sleepiness.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Sleepiness.Systems;

public sealed partial class SonarDamageThresholdSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnGetState(Entity<SonarDamageThresholdComponent> ent, ref ComponentGetState args) =>
        args.State = new SonarDamageThresholdComponentState(ent.Comp.DestructionThreshold);

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<SonarDamageThresholdComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SonarDamageThresholdComponentState state)
            return;

        ent.Comp.DestructionThreshold = state.DestructionThreshold;
    }
}
