using Content.Server._NullLink.PlayerData;
using Content.Server.Access.Systems;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Roles.Ranks;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Starlight.Roles.Ranks;

public sealed partial class RankSystem : EntitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly NullLinkPlayerManager _linkPlayerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!_idCard.TryFindIdCard(ev.Mob, out var idCard))
            return;

        if (ev.JobId == null)
            return;

        if (!_prototype.TryIndex(ev.JobId, out JobPrototype? job))
            return;

        if (job.Ranks == null)
            return;

        if (!_linkPlayerManager.TryGetPlayerData(ev.Player.UserId, out PlayerData? playerData))
            return;

        RankPrototype? highestRank = null;
        foreach (var rankId in job.Ranks)
        {
            if (!_prototype.TryIndex(rankId, out RankPrototype? rank))
                continue;

            if (!_prototype.TryIndex(rank.Requirement, out RoleRequirementPrototype? roleRequirement))
                continue;

            if (!playerData.Roles.Overlaps(roleRequirement.Roles))
                continue;

            highestRank ??= rank;

            if (highestRank.Priority < rank.Priority)
                highestRank = rank;
        }

        if (highestRank != null)
        {
            idCard.Comp.JobTitle = highestRank.Name;
            idCard.Comp.JobIcon = highestRank.Icon;
        }

    }
}
