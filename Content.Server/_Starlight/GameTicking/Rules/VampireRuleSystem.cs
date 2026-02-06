using Content.Server.Antag;
using Content.Server.Bible.Components;
using Content.Server._Starlight.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared._Starlight.Antags.Vampires.Components;
using System.Text;
using Robust.Shared.Audio;
using Content.Server.GameTicking.Rules;
using Robust.Shared.Random;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed partial class VampireRuleSystem : GameRuleSystem<VampireRuleComponent>
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly ObjectivesSystem _objective = default!;

    public readonly SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/_Starlight/Ambience/Antag/vampire_start.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VampireRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<VampireRuleComponent, ObjectivesTextPrependEvent>(OnTextPrepend);
    }

    private void OnSelectAntag(EntityUid uid, VampireRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
        => MakeVampire(args.EntityUid, comp);

    public bool MakeVampire(EntityUid target, VampireRuleComponent rule)
    {
        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        // Roundstart chaplains shouldnt be vampires.
        if (HasComp<BibleUserComponent>(target))
        {
            _role.MindRemoveRole((mindId, mind), "MindRoleVampire");
            return false;
        }
        
        var meta = MetaData(target);
        var name = meta?.EntityName ?? "Unknown";
        var briefing = Loc.GetString("vampire-role-greeting", ("name", name));
        _antag.SendBriefing(target, briefing, Color.Yellow, BriefingSound);

        if (
             _role.MindHasRole<VampireRoleComponent>(mindId, out var vampRole)
          && _role.MindHasRole<RoleBriefingComponent>(mindId, out var briefingComp)
        )
        {
            AddComp<RoleBriefingComponent>(vampRole.Value.Owner);
            Comp<RoleBriefingComponent>(vampRole.Value.Owner).Briefing = briefing;
        }

        EnsureComp<VampireComponent>(target);

        rule.VampireMinds.Add(mindId);

        foreach (var objective in rule.BaseObjectives)
            _mind.TryAddObjective(mindId, mind, objective);

        var rng = new Random();
        if (rule.EscapeObjectives.Count > 0)
        {
            var obj = rng.Pick(rule.EscapeObjectives);
            _mind.TryAddObjective(mindId, mind, obj);
        }

        if (rule.StealObjectives.Count > 0)
        {
            var obj = rng.Pick(rule.StealObjectives);
            _mind.TryAddObjective(mindId, mind, obj);
        }

        return true;
    }

    private void OnTextPrepend(EntityUid uid, VampireRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        var mostDrainedName = string.Empty;
        var mostDrained = 0f;
        var totalBlood = 0f;

        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var vampUid, out var vamp))
        {
            if (!_mind.TryGetMind(vampUid, out var mindId, out var mind))
                continue;

            if (!TryComp(vampUid, out MetaDataComponent? meta))
                continue;

            totalBlood += vamp.TotalBlood;

            if (vamp.TotalBlood > mostDrained)
            {
                mostDrained = vamp.TotalBlood;
                mostDrainedName = _objective.GetTitle((mindId, mind), meta.EntityName);
            }
        }

        var sb = new StringBuilder();

        // Display blood statistics based on total amount drained
        if (totalBlood > 0)
        {
            var category = totalBlood switch
            {
                < 500 => "low",
                < 1000 => "medium",
                < 2000 => "high",
                _ => "critical"
            };
            sb.AppendLine(Loc.GetString($"roundend-prepend-vampire-drained-{category}", ("blood", (int)totalBlood)));
        }

        sb.AppendLine(Loc.GetString($"roundend-prepend-vampire-drained{(!string.IsNullOrWhiteSpace(mostDrainedName) ? "-named" : "")}", ("name", mostDrainedName), ("number", (int)mostDrained)));

        args.Text = sb.ToString();
    }
}
