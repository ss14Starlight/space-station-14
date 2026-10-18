using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Content.Server._Starlight.CosmicCult.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Content.Server._Starlight.Administration.Systems;
using Robust.Shared.Toolshed;
// ReSharper disable InvertIf - Better to just not since new verbs can be added at any time and un-inverting these is a bit annoying.

namespace Content.Server._Starlight.Administration;

public sealed partial class StarlightAdminVerbSystem : EntitySystem
{
    [Dependency] private AutoDiscordLogSystem _autolog = null!;
    [Dependency] private AntagSelectionSystem _antagSelection = null!;
    [Dependency] private ToolshedManager _tsh = null!;
    [Dependency] private IAdminManager _adminManager = null!;

    public override void Initialize()
        => SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);

    private void GetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.IsAdmin(player)) return;

        #region CosmicCult

        if (_adminManager.HasAdminFlag(player, AdminFlags.Fun))
            if (HasComp<MindContainerComponent>(args.Target) &&
                TryComp<ActorComponent>(args.Target, out var targetActor))
            {
                var targetPlayer = targetActor.PlayerSession;

                var cosmicCultName = Loc.GetString("admin-verb-text-make-cosmiccultist");
                Verb cosmicCult = new()
                {
                    Text = cosmicCultName,
                    Category = VerbCategory.Antag,
                    Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Starlight/CosmicCult/Icons/antag_icons.rsi"),
                        "CosmicCult"),
                    Act = () =>
                    {
                        _antagSelection.ForceMakeAntag<CosmicCultRuleComponent>(targetPlayer, "CosmicCult",
                            "CosmicAntagCultist");
                        _autolog.LogToDiscord(
                            string.Join(": ", cosmicCultName, Loc.GetString("admin-verb-make-cosmiccultist")),
                            player.Name);
                    },
                    Impact = LogImpact.High,
                    Message = string.Join(": ", cosmicCultName, Loc.GetString("admin-verb-make-cosmiccultist")),
                };
                args.Verbs.Add(cosmicCult);
            }

        #endregion

        #region Entity Marking

        if (_adminManager.HasAdminFlag(player, AdminFlags.Admin))
        {
            Verb setMarked = new()
            {
                Text = Loc.GetString("toolshed-verb-set-marked"),
                Message = Loc.GetString("toolshed-verb-set-marked-description"),
                Category = VerbCategory.Mark,
                Act = () => _tsh.InvokeCommand(player, "mark:set", args.Target, out _),
                Impact = LogImpact.Low,
                Priority = 4,
                Icon = new SpriteSpecifier.Texture(
                    new ResPath("/Textures/_Starlight/Interface/Misc/crosshair_pointers.rsi/cross_dot.png"))
            };
            args.Verbs.Add(setMarked);

            Verb appendMarked = new()
            {
                Text = Loc.GetString("toolshed-verb-append-marked"),
                Message = Loc.GetString("toolshed-verb-append-marked-description"),
                Category = VerbCategory.Mark,
                Act = () => _tsh.InvokeCommand(player, "mark:append", args.Target, out _),
                Impact = LogImpact.Low,
                Priority = 3,
                Icon = new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/insert.svg.192dpi.png"))
            };
            args.Verbs.Add(appendMarked);

            Verb clearMarked = new()
            {
                Text = Loc.GetString("toolshed-verb-clear-marked"),
                Message = Loc.GetString("toolshed-verb-clear-marked-description"),
                Category = VerbCategory.Mark,
                Act = () => _tsh.InvokeCommand(player, "mark:clear", null, out _),
                Impact = LogImpact.Low,
                Priority = 1,
                Icon = new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png"))
            };
            args.Verbs.Add(clearMarked);

            _tsh.InvokeCommand(player, "marked", null, out var marked);

            if (marked is List<EntityUid> uids && uids.Contains(args.Target))
            {
                Verb rmMarked = new()
                {
                    Text = Loc.GetString("toolshed-verb-rm-marked"),
                    Message = Loc.GetString("toolshed-verb-rm-marked-description"),
                    Category = VerbCategory.Mark,
                    Act = () => _tsh.InvokeCommand(player, "mark:rm", args.Target, out _),
                    Impact = LogImpact.Low,
                    Priority = 2,
                    Icon = new SpriteSpecifier.Texture(
                        new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"))
                };
                args.Verbs.Add(rmMarked);
            }
        }

        #endregion
    }
}
