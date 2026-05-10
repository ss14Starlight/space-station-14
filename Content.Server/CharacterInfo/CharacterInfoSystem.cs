using Content.Server._Starlight.Objectives.Events;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.CharacterInfo;
using Content.Shared.CollectiveMind;
using Content.Shared.Objectives;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.CharacterInfo;

public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly SharedCollectiveMindSystem _collectiveMind = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(OnRequestCharacterInfoEvent);
    }

    private void OnRequestCharacterInfoEvent(RequestCharacterInfoEvent msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue
            || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;

        var objectives = new Dictionary<string, List<ObjectiveInfo>>();
        var jobTitle = Loc.GetString("character-info-no-profession");
        string? briefing = null;

        // 🌟Starlight🌟 start
        var collectiveMinds = new Dictionary<CollectiveMindPrototype, CollectiveMindMemberData>();
        if (TryComp<CollectiveMindComponent>(entity, out var mindsComp))
        {
            foreach (var collectiveMind in mindsComp.Minds)
            {
                if (!_collectiveMind.CheckCanSpeak(entity, collectiveMind.Key))
                    continue;

                collectiveMinds.Add(collectiveMind.Key, collectiveMind.Value);
            }
        }

        var @event = new CollectObjectivesEvent(objectives);
        RaiseLocalEvent(entity, ref @event);
        // 🌟Starlight🌟 end

        if (_minds.TryGetMind(entity, out var mindId, out var mind))
        {
            // Get objectives
            foreach (var objective in mind.Objectives)
            {
                var info = _objectives.GetInfo(objective, mindId, mind);
                if (info == null)
                    continue;

                // group objectives by their issuer
                var issuer = Comp<ObjectiveComponent>(objective).LocIssuer;
                if (!objectives.ContainsKey(issuer))
                    objectives[issuer] = new List<ObjectiveInfo>();
                objectives[issuer].Add(info.Value);
            }

            if (_jobs.MindTryGetJobName(mindId, out var jobName))
                jobTitle = jobName;

            // Get briefing
            briefing = _roles.MindGetBriefing(mindId);
        }

        RaiseNetworkEvent(new CharacterInfoEvent(GetNetEntity(entity), jobTitle, objectives, briefing, collectiveMinds), args.SenderSession);
    }
}
