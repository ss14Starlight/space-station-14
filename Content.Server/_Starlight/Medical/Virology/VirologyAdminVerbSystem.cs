using Content.Server.Administration.Managers;
using Content.Shared._Starlight.Medical.Virology;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Medical.Virology;
public sealed partial class VirologyAdminVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private PathogenSystem _pathogen = default!;


    private static readonly VerbCategory VirologyCategory =
        new("verb-categories-virology", "/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddVirologyVerbs);
    }

    private void AddVirologyVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_adminManager.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun))
        {
            return;
        }

        if (!_pathogen.CanHost(args.Target))
            return;

        var target = args.Target;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("verb-virology-cure-text"),
            Category = VirologyCategory,
            Priority = 1,
            Act = () =>
            {
                if (!TryComp<PathogenInfectionComponent>(target, out var infections))
                    return;

                foreach (var infection in infections.Infections.ToArray())
                    _pathogen.Cure(target, infection.Pathogen, grantImmunity: true, cause: "admin verb");
            },
            Impact = LogImpact.Medium,
            Message = Loc.GetString("verb-virology-cure-message"),
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("verb-virology-immune-text"),
            Category = VirologyCategory,
            Act = () => EnsureComp<PathogenImmunityComponent>(target).Total = true,
            Impact = LogImpact.Medium,
            Message = Loc.GetString("verb-virology-immune-message"),
        });
    }
}
