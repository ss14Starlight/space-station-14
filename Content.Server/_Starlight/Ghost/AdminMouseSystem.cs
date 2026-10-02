using Content.Server._Starlight.Administration.Systems;
using Content.Server.Administration.Managers;
using Content.Server.Polymorph.Systems;
using Content.Shared._NullLink;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Ghost;

public sealed partial class AdminMouseSystem : EntitySystem
{
    [Dependency] private ISharedNullLinkPlayerRolesReqManager _playerRoles = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private PolymorphSystem _polymorphSystem = default!;
    [Dependency] private AutoDiscordLogSystem _autolog = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, GetVerbsEvent<Verb>>(OnGetInteractionVerbs);
    }

    private void OnGetInteractionVerbs(EntityUid uid, GhostComponent component, ref GetVerbsEvent<Verb> args)
    {
        var user = args.User;

        if (args.Target != user || !HasComp<GhostComponent>(user))
            return;

        if (_admin.IsAdmin(user, true))
        {
            var adminName = Loc.GetString("admin-verb-text-make-adminmouse");
            Verb admin = new()
            {
                Text = adminName,
                Category = VerbCategory.Smite,
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Starlight/Mobs/Animals/mouse.rsi"), "icon-0"),
                Act = () =>
                {
                    _polymorphSystem.PolymorphEntity(user, "AdminMouse");
                    _autolog.LogToDiscord(Loc.GetString("autolog-admin-mouse"), ToPrettyString(user));
                },
                Impact = LogImpact.Extreme,
                Message = string.Join(": ", adminName, Loc.GetString("admin-verb-make-adminmouse")),
            };
            args.Verbs.Add(admin);
        }

        if (_playerRoles.IsMentor(user))
        {
            var mentorName = Loc.GetString("admin-verb-text-make-mentormouse");
            Verb mentor = new()
            {
                Text = mentorName,
                Category = VerbCategory.Smite,
                Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Starlight/Mobs/Animals/mouse.rsi"), "icon-0"),
                Act = () =>
                {
                    _polymorphSystem.PolymorphEntity(user, "MentorMouse");
                    _autolog.LogToDiscord(Loc.GetString("autolog-mentor-mouse"), ToPrettyString(user));
                },
                Impact = LogImpact.Extreme,
                Message = string.Join(": ", mentorName, Loc.GetString("admin-verb-make-mentormouse")),
            };
            args.Verbs.Add(mentor);
        }
    }
}
