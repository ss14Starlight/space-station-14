using Content.Server.Antag;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared._Starlight.Devil;
using Content.Shared._Starlight.Roles.Components;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class DevilRuleSystem : GameRuleSystem<DevilRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<DevilRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void AfterAntagSelected(EntityUid uid, DevilRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        EnsureComp<DevilComponent>(args.EntityUid);
        _antag.SendBriefing(args.EntityUid, MakeBriefing(), null, null);
    }

    private void OnGetBriefing(EntityUid uid, DevilRoleComponent comp, ref GetBriefingEvent args) => args.Append(MakeBriefing());

    private string MakeBriefing() => Loc.GetString("devil-role-briefing");
}
