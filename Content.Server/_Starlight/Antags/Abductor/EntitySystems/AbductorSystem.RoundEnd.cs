using Content.Server.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Network;
using Content.Shared._Starlight.Antags.Abductor.Components;

namespace Content.Server._Starlight.Antags.Abductor.EntitySystems;

public sealed partial class AbductorSystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    private void InitializeRoundEnd() => SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var uniqueVictims = new HashSet<NetEntity>();
        var maxAbducted = 0;

        var query = EntityQueryEnumerator<AbductConditionComponent>();
        while (query.MoveNext(out var _, out var cond))
        {
            maxAbducted = Math.Max(maxAbducted, cond.Abducted);
            foreach (var victim in cond.AbductedHashs)
            {
                if (victim == NetEntity.Invalid)
                    continue;

                uniqueVictims.Add(victim);
            }
        }

        var total = Math.Max(uniqueVictims.Count, maxAbducted);

        var abductors = new List<string>();

        var abductorsSeen = new HashSet<NetUserId>();

        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var _, out var mind))
        {
            if (mind.Objectives.Count == 0)
                continue;

            var isAbductor = false;
            foreach (var objective in mind.Objectives)
            {
                if (HasComp<AbductConditionComponent>(objective))
                {
                    isAbductor = true;
                    break;
                }
            }

            if (!isAbductor)
                continue;

            var userId = mind.UserId ?? mind.OriginalOwnerUserId;
            if (userId != null && _playerManager.TryGetPlayerData(userId.Value, out var pdata))
            {
                var name = pdata.ContentData()?.Name;

                if (!string.IsNullOrWhiteSpace(name) && abductorsSeen.Add(userId.Value))
                    abductors.Add(name!);
            }
        }

        if (total > 0 || abductors.Count > 0)
        {
            ev.AddLine(Loc.GetString("round-end-prepend-abductor-abducted", ("number", total)));

            if (abductors.Count > 0)
            {
                ev.AddLine(Loc.GetString("round-end-prepend-abductor-were"));
                foreach (var name in abductors)
                {
                    ev.AddLine(Loc.GetString("round-end-prepend-abductor-name", ("name", name)));
                }
            }
        }
    }
}
