using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.IdentityManagement;
using Robust.Shared.Audio;
using Robust.Shared.Timing;
using System.Linq;
using Content.Server._Starlight.Language;
using Content.Shared.Mind.Components;
using Content.Server._Starlight.Xeno.Components;
using Content.Shared._Starlight.Xeno.Components;
using Content.Shared.Actions;
using Content.Server.Antag;
using Content.Shared.Mind;

namespace Content.Server._Starlight.Xeno;

/// <summary>
/// Where all the main stuff for Cosmic Cultists happens.
/// </summary>
public sealed partial class XenoDemocracyRuleSystem : GameRuleSystem<XenoDemocracyRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LanguageSystem _languageSystem = default!;
    [Dependency] private SharedActionsSystem _action = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    private ISawmill _sawmill = default!;

    private readonly SoundSpecifier _voiceElectedAlert = new SoundCollectionSpecifier("XenoRoarEcho");

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("xenodemocracy");

        SubscribeLocalEvent<XenoDemocracyComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<XenoDemocracyComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<XenoDemocracyComponent, MindRemovedMessage>(HandleMindRemoved);
    }

    protected override void Started(EntityUid uid, XenoDemocracyRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        => component.VoiceVoteTimer = _timing.CurTime + component.VoiceVoteDelay;

    protected override void ActiveTick(EntityUid uid, XenoDemocracyRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (component.VoiceVoteTimer is { } voteTimer && _timing.CurTime >= voteTimer)
        {
            component.VoiceVoteTimer = null;
            VoiceVote();
        }
    }

    private void VoiceVote()
    {
        var ruleQuery = QueryActiveRules();
        var foundRule = false;
        XenoDemocracyRuleComponent? rule;
        while (ruleQuery.MoveNext(out var ruleUid, out var activeRuleComp, out rule, out var gameRule))
        {
            foundRule = true;

            // If we currently have a Voice, why are we even here?
            if (rule.HasVoice)
                return;

            break;
        }

        if (!foundRule)
            return;

        //Tally up all the votes!
        var votes = new Dictionary<EntityUid, int>(); //using a dictionary because we want the IDs to be unique!
        var voterQuery = EntityQueryEnumerator<XenoDemocracyComponent, MetaDataComponent>();

        while (voterQuery.MoveNext(out var voter, out _, out var metadata))
        {
            if(!TryComp(voter, out XenoDemocracyComponent? comp) || comp == null)
                continue;

            votes.TryAdd(voter, 0);
            if(!comp.CurrentVoteHolder.HasValue)
                votes[voter]+= 1;
            else if(!votes.TryAdd(comp.CurrentVoteHolder.Value, 1))
                votes[comp.CurrentVoteHolder.Value]+= 1;
        }

        //Did no one vote somehow?
        if (votes.Count == 0)
        {
            _sawmill.Info("Voice vote tallying started, but found no votes");
            rule?.VoiceVoteTimer = _timing.CurTime.Add(rule.VoiceVoteDelay); //Retry later.
            return;
        }

        //Ranking the votes by flipping their key-value pairing into a sorted list
        var voteRankings = new List<KeyValuePair<int, EntityUid>>();
        foreach(KeyValuePair<EntityUid, int> entry in votes)
        {
            voteRankings.Add(new KeyValuePair<int, EntityUid>(entry.Value, entry.Key));
        }
        voteRankings = [.. voteRankings.OrderByDescending(v => v.Key)];
        while(voteRankings.Count > 0)
        {
            var selectedVoice = voteRankings.First();
            voteRankings.Remove(selectedVoice);

            if(!TryComp(selectedVoice.Value, out XenoDemocracyComponent? comp) || !_mind.TryGetMind(selectedVoice.Value, out _, out _))
                continue;
            _languageSystem.AddLanguage(selectedVoice.Value, comp.VoiceLanguage);
            _languageSystem.RemoveLanguage(selectedVoice.Value, comp.NonVoiceLanguage, removeUnderstood: false);
            comp.IsVoice = true;
            rule?.HasVoice = true;

            _adminLogger.Add(LogType.Vote, LogImpact.Medium, $"Xenocratic vote finished: {Identity.Entity(selectedVoice.Value, EntityManager)} is now Voice.");
            _antag.SendBriefing(selectedVoice.Value, Loc.GetString("xeno-vote-voice-briefing"), Color.FromHex("#6f00ff"), _voiceElectedAlert);
            break;
        }

        if(rule != null && !rule.HasVoice)
        {
            _sawmill.Info("Voice vote somehow finished without eligible candidates.");
        }

        //Once done, remove vote action from all eligible voters
        voterQuery = EntityQueryEnumerator<XenoDemocracyComponent, MetaDataComponent>();

        while (voterQuery.MoveNext(out var voter, out _, out var metadata))
        {
            if(!TryComp(voter, out XenoDemocracyComponent? comp) || comp == null)
                continue;

            if (!Deleted(voter) && comp.ActionEntity is not null)
                _action.RemoveAction((voter, null), comp.ActionEntity);
        }
    }

    private void HandleMindRemoved(Entity<XenoDemocracyComponent> ent, ref MindRemovedMessage args)
    {
        if (!ent.Comp.IsVoice) // Target isn't the Voice, no need to revote
            return;

        // Tell the rule we no longer have a Voice
        var ruleQuery = QueryActiveRules();
        while (ruleQuery.MoveNext(out _, out _, out var rule, out _))
        {
            rule.HasVoice = false;
        }

        VoiceVote(); //Get the next in line!
    }

    //Need to do this for comp shutdown, too, in case the component is forcefully removed
    private void OnComponentShutdown(Entity<XenoDemocracyComponent> ent, ref ComponentShutdown args)
    {
        if (!Deleted(ent.Owner) && ent.Comp.ActionEntity is not null)
            _action.RemoveAction((ent.Owner, null), ent.Comp.ActionEntity);

        if (!ent.Comp.IsVoice) // Target isn't the Voice, no need to revote
            return;

        // Tell the rule we no longer have a Voice
        var ruleQuery = QueryActiveRules();
        while (ruleQuery.MoveNext(out _, out _, out var rule, out _))
        {
            rule.HasVoice = false;
        }

        VoiceVote(); //Get the next in line!
    }

    private void OnComponentStartup(EntityUid uid, XenoDemocracyComponent component, ComponentStartup _)
    {
        var ruleQuery = QueryActiveRules();
        var foundRule = false;
        while (ruleQuery.MoveNext(out var ruleUid, out var activeRuleComp, out var rule, out var gameRule))
        {
            foundRule = true;

            // If we currently have a Voice, don't add the vote action.
            if (rule.HasVoice)
                return;

            // If we *don't* have a voice, check if we are supposed to have one... And then also don't add the action, as it will no longer be needed after.
            if (rule.VoiceVoteTimer is { } voteTimer && _timing.CurTime >= voteTimer)
            {
                VoiceVote();
            }
            break;
        }

        if (!foundRule)
            return;

        _action.AddAction(uid, ref component.ActionEntity, component.XenoDemocracyVoteAction);

        Dirty(uid, component);
    }
}
