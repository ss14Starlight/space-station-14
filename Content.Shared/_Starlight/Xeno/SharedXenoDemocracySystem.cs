using Content.Shared._Starlight.Xeno;
using Content.Shared._Starlight.Xeno.Components;
using Content.Shared.Actions;

namespace Content.Shared._Starlight.Xemo;

public abstract partial class SharedXenoDemocracySystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoDemocracyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<XenoDemocracyVoteActionEvent>(OnVote);
    }

    private void OnShutdown(EntityUid uid, XenoDemocracyComponent component, ComponentShutdown args)
    {
        if (Deleted(uid) || component.ActionEntity is null)
            return;

        _action.RemoveAction((uid, null), component.ActionEntity);
    }

    private void OnVote(XenoDemocracyVoteActionEvent args)
    {
        _sawmill.Log(LogLevel.Error, "Vote function called");

        if(!TryComp(args.Performer, out XenoDemocracyComponent? xeno) || xeno == null)
            return;
        _sawmill.Log(LogLevel.Error, "Comp holder performer");

        //No voting for Ian please, much as he deserves it <3
        if(!HasComp<XenoDemocracyComponent>(args.Target))
            return;
        _sawmill.Log(LogLevel.Error, "Comp holder target");

        xeno.CurrentVoteHolder = args.Target;
    }
}
