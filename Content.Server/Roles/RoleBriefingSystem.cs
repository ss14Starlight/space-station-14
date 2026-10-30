using Content.Shared.Roles.Components;

namespace Content.Server.Roles;

public sealed partial class RoleBriefingSystem : EntitySystem
{
    [Dependency] private RoleSystem _roles = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoleBriefingComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void OnGetBriefing(EntityUid uid, RoleBriefingComponent comp, ref GetBriefingEvent args)
    {
        // Starlight, antagonists use their role briefing instead of the crew alignment briefing.
        if (HasComp<JobRoleComponent>(uid) && _roles.MindIsAntagonist(args.Mind.Owner))
            return;

        args.Append(Loc.GetString(comp.Briefing));
    }
}
