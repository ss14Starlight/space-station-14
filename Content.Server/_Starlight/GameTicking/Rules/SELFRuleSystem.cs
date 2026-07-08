using Content.Server.Antag;
using Content.Server.Roles;
using Content.Server.GameTicking.Rules;
using Content.Shared._Starlight.Antags.SELF;
using SELFRuleComponent = Content.Server._Starlight.GameTicking.Rules.Components.SELFRuleComponent;
using Content.Shared._Starlight.Roles;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class SELFRuleSystem : GameRuleSystem<SELFRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SELFRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<SELFAgentRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon SELF activation
    private void AfterAntagSelected(Entity<SELFRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;

        _antag.SendBriefing(ent, MakeBriefing(ent), null, null);

        // Mark the player entity as a SELF agent for whitelists/blacklists
        EnsureComp<SELFAgentComponent>(ent);
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<SELFAgentRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent == null)
            return;

        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid _)
    {
        var briefing = Loc.GetString("self-role-greeting-human");

            briefing += "\n \n" + Loc.GetString("self-role-greeting-equipment") + "\n";

        return briefing;
    }
}
