using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Content.Server._ST.CosmicCult.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Server._Starlight.Administration.Systems;

namespace Content.Server._ST.Administration;

public sealed partial class StellarAdminVerbSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antagSelection = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly AutoDiscordLogSystem _autolog = default!; //Starlight

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);
    }

    private void GetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            return;

        if (!HasComp<MindContainerComponent>(args.Target) || !TryComp<ActorComponent>(args.Target, out var targetActor))
            return;

        var targetPlayer = targetActor.PlayerSession;

        var cosmicCultName = Loc.GetString("admin-verb-text-make-cosmiccultist");
        Verb cosmiccult = new()
        {
            Text = cosmicCultName,
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new("/Textures/_ST/CosmicCult/Icons/antag_icons.rsi"), "CosmicCult"),
            Act = () =>
            {
                _antagSelection.ForceMakeAntag<CosmicCultRuleComponent>(targetPlayer, "CosmicCult");
                _autolog.LogToDiscord(string.Join(": ", cosmicCultName, Loc.GetString("admin-verb-make-cosmiccultist")), player.Name); //Starlight
            },
            Impact = LogImpact.High,
            Message = string.Join(": ", cosmicCultName, Loc.GetString("admin-verb-make-cosmiccultist")),
        };
        args.Verbs.Add(cosmiccult);
    }
}
