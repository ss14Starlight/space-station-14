using Content.Shared._Starlight.Antags.Vampires.Components.Classes;

namespace Content.Shared._Starlight.Antags.Vampires.Systems;

public sealed partial class SharedHemomancerSystem : EntitySystem
{
    [Dependency] private SharedVampireActionUseSystem _vampireActions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireHemomancerClawsActionEvent>(OnHemomancerClaws);
    }

    private void OnHemomancerClaws(VampireHemomancerClawsActionEvent args)
    {
        var uid = args.Performer;
        var action = args.Action.Owner;
        if (args.Handled
            || !Exists(action)
            || !_vampireActions.TryUse(uid, action))
        {
            return;
        }

        if (TryComp<HemomancerComponent>(uid, out var hemomancer))
        {
            hemomancer.HemomancerClawsActive = true;
            Dirty(uid, hemomancer);
        }

        var activated = new VampireHemomancerClawsActivatedEvent(uid);
        RaiseLocalEvent(uid, ref activated, true);
        args.Handled = true;
    }
}
