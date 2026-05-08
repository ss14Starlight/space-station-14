using Content.Shared._Starlight.Antags.Vampires.Components;
using Content.Shared._Starlight.Antags.Vampires.Components.Classes;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public abstract class SharedHemomancerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireHemomancerClawsActionEvent>(OnHemomancerClaws);
    }

    protected virtual bool TryUseHemomancerClaws(EntityUid uid, EntityUid actionEntity)
        => false;

    protected virtual void OnPredictedHemomancerClaws(EntityUid uid, ActiveVampireHemomancerClawsComponent active, VampireHemomancerClawsActionEvent args)
    {
    }

    private void OnHemomancerClaws(VampireHemomancerClawsActionEvent args)
    {
        var uid = args.Performer;
        var action = args.Action.Owner;
        if (args.Handled
            || !Exists(action)
            || !TryUseHemomancerClaws(uid, action))
        {
            return;
        }

        var active = EnsureComp<ActiveVampireHemomancerClawsComponent>(uid);

        if (TryComp<HemomancerComponent>(uid, out var hemomancer))
        {
            hemomancer.HemomancerClawsActive = true;
            Dirty(uid, hemomancer);
        }

        OnPredictedHemomancerClaws(uid, active, args);
        args.Handled = true;
    }
}
