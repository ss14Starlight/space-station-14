using Content.Server.Administration.Logs;
using Content.Shared._Starlight.CustomObjectiveSummary;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._Starlight.CustomObjectiveSummary;

public sealed class CustomObjectiveSummarySystem : EntitySystem
{
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacShuttleLeftEvent>(OnEvacShuttleLeft);

        _net.RegisterNetMessage<CustomObjectiveClientSetObjective>(OnCustomObjectiveFeedback);
    }

    private void OnCustomObjectiveFeedback(CustomObjectiveClientSetObjective msg)
    {
        if (!_mind.TryGetMind(msg.MsgChannel.UserId, out var mind))
            return;

        if (mind.Value.Comp.Objectives.Count == 0)
            return;

        var comp = EnsureComp<CustomObjectiveSummaryComponent>(mind.Value);

        comp.ObjectiveSummary = msg.Summary;
        Dirty(mind.Value.Owner, comp);

        _adminLog.Add(LogType.ObjectiveSummary, $"{ToPrettyString(mind.Value.Comp.OwnedEntity)} wrote objective summery: {msg.Summary}");
    }

    private void OnEvacShuttleLeft(EvacShuttleLeftEvent args)
    {
        var allMinds = _mind.GetAliveHumans();

        // Assumes the assistant is still there at the end of the round.
        foreach (var mind in allMinds)
        {
            // Only send the popup to people with objectives.
            if (mind.Comp.Objectives.Count == 0)
                continue;

            // Get the session from the mind's owned entity
            if (mind.Comp.OwnedEntity == null || !_players.TryGetSessionByEntity(mind.Comp.OwnedEntity.Value, out var session))
                continue;

            RaiseNetworkEvent(new CustomObjectiveSummaryOpenMessage(), session);
        }
    }
}
