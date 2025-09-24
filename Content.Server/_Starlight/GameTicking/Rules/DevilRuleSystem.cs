using Content.Server.Antag;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Shared._Starlight.Devil;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class DevilRuleSystem : GameRuleSystem<DevilRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<DevilRuleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    private void AfterAntagSelected(EntityUid uid, DevilRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        EnsureComp<DevilComponent>(uid);
        _antag.SendBriefing(args.EntityUid, MakeBriefing(), null, null);
    }

    private void OnGetBriefing(EntityUid uid, DevilRuleComponent comp, ref GetBriefingEvent args)
    {
        args.Append(MakeBriefing());
    }

    private string MakeBriefing()
    {
        return Loc.GetString("devil-role-briefing");
    }
}