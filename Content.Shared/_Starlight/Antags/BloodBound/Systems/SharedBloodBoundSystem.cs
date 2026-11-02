using Content.Shared._Starlight.Antags.BloodBound.Components;
using Content.Shared.Antag;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Antags.BloodBound.Systems;

public abstract partial class SharedBloodBoundSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodBoundComponent, ComponentGetStateAttemptEvent>(OnAttemptGetState);
    }

    private void OnAttemptGetState(Entity<BloodBoundComponent> entity, ref ComponentGetStateAttemptEvent args)
    {
        if (args.Player?.AttachedEntity is not {} uid)
            return;

        args.Cancelled = !HasComp<BloodBoundComponent>(uid) && !HasComp<ShowAntagIconsComponent>(uid);
    }
}
