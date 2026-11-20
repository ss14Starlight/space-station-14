using Content.Shared._Starlight.Xeno.Components;

namespace Content.Shared._Starlight.Xeno;

public sealed partial class SharedXenoDemocracySystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoDemocracyVoteActionEvent>(OnVote);
    }

    private void OnVote(XenoDemocracyVoteActionEvent args)
    {
        if (args.Handled)
            return;

        if(!TryComp(args.Performer, out XenoDemocracyComponent? xeno) || xeno == null)
            return;

        //No voting for Ian please, much as he deserves it <3
        if(!HasComp<XenoDemocracyComponent>(args.Target))
            return;

        xeno.CurrentVoteHolder = args.Target;
        args.Handled = true;
    }
}
