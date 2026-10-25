using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server._Starlight.MindCollective;

public sealed partial class MindCollectiveSystem
{
    [Dependency] private IAdminManager _collectiveAdmins = default!;

    public void OpenAdminUi(ICommonSession player, EntityUid? target = null)
    {
        if (_collectiveAdmins.HasAdminFlag(player, AdminFlags.Debug))
            _membershipEui.OpenEui(new MindCollectiveAdminEui(this, target), player);
    }

    [SubscribeLocalEvent]
    private void OnCollectiveDebugVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor)
            || !_collectiveAdmins.HasAdminFlag(actor.PlayerSession, AdminFlags.Debug))
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("mind-collective-admin-title"),
            Category = VerbCategory.Debug,
            Act = () => OpenAdminUi(actor.PlayerSession, args.Target),
        });
    }
}
