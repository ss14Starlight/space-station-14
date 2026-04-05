using Content.Shared.Hands.EntitySystems;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Humanoid;
using Content.Shared.Roles.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules;
using Content.Shared._Starlight.Roles.Components;
using SELFRuleComponent = Content.Server._Starlight.GameTicking.Rules.Components.SELFRuleComponent;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class SELFRuleSystem : GameRuleSystem<SELFRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

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
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<SELFAgentRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent == null)
            return;

        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        var briefing = Loc.GetString("self-role-greeting-human");

            briefing += "\n \n" + Loc.GetString("self-role-greeting-equipment") + "\n";

        return briefing;
    }
}
