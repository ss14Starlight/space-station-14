using Content.Server.Antag.Components;
using Content.Shared.Antag;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Preferences;

namespace Content.Server.Antag;

public sealed partial class AntagSelectionSystem
{
    readonly int _effectivePlayerCutoff = 30; // The number of online players at which unready players start counting as effectively ready
    readonly double _unreadyPlayerMultiplier = 0.25; // The fraction of unready players that count as effectively ready when above the cutoff
    private int GetEffectivePlayerCountPlayerRatio(int activePlayers)
    {
        var onlinePlayers = _playerManager.Sessions.Length;

        if (onlinePlayers < _effectivePlayerCutoff)
            return activePlayers;

        var inactivePlayers = Math.Max(0, onlinePlayers - activePlayers);

        // Count 25% of lobby/spectating/unready players.
        return activePlayers + (int)(inactivePlayers * _unreadyPlayerMultiplier);
    }

    /// <summary>
    /// Gets the number of still-available ghost roles reserving slots for an antagonist type.
    /// </summary>
    [PublicAPI]
    public int GetPendingAntagGhostRoleCount(
        Entity<AntagSelectionComponent> gameRule,
        ProtoId<AntagSpecifierPrototype> proto)
    {
        var count = 0;

        foreach (var ghostRole in _ghostRole.GhostRoles)
        {
            if (ghostRole.Comp.Taken ||
                !TryComp<GhostRoleAntagSpawnerComponent>(ghostRole.Owner, out var spawner) ||
                spawner.Rule != gameRule.Owner ||
                spawner.Definition != proto)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Merges the job whitelist and blacklist of a given antag definition with the existing job whitelist and blacklist for a player.
    /// </summary>
    /// <param name="jobs">The existing job whitelist and blacklist for a player.</param>
    /// <param name="definition">The antag definition containing its own job whitelist and blacklist.</param>
    /// <returns>The merged job whitelist and blacklist.</returns>
    private static (HashSet<ProtoId<JobPrototype>>? Whitelist, HashSet<ProtoId<JobPrototype>>? Blacklist)
        MergeAntagJobs(
            (HashSet<ProtoId<JobPrototype>>? Whitelist, HashSet<ProtoId<JobPrototype>>? Blacklist) jobs,
            AntagSpecifierPrototype definition)
    {
        if (definition.JobWhitelist != null)
        {
            if (jobs.Whitelist == null)
                jobs.Whitelist = new HashSet<ProtoId<JobPrototype>>(definition.JobWhitelist);
            else
                jobs.Whitelist.IntersectWith(definition.JobWhitelist);
        }

        if (definition.JobBlacklist != null)
        {
            if (jobs.Blacklist == null)
                jobs.Blacklist = new HashSet<ProtoId<JobPrototype>>(definition.JobBlacklist);
            else
                jobs.Blacklist.UnionWith(definition.JobBlacklist);
        }

        return jobs;
    }

    /// <summary>
    /// Returns whether this specific character profile can be used for an antag definition.
    /// Account-level antag eligibility is not sufficient here because another enabled character may
    /// be the profile that actually satisfies the preference or a profile-specific requirement.
    /// TLDR: blame multi-slot
    /// </summary>
    [PublicAPI]
    public bool IsProfileValidForAntag(
        ICommonSession session,
        HumanoidCharacterProfile profile,
        ProtoId<AntagSpecifierPrototype> definition)
    {
        return Proto.Resolve(definition, out var antag) && IsProfileValidForAntag(session, profile, antag);
    }

    /// <inheritdoc cref="IsProfileValidForAntag(ICommonSession,HumanoidCharacterProfile,ProtoId{AntagSpecifierPrototype})"/>
    [PublicAPI]
    public bool IsProfileValidForAntag(
        ICommonSession session,
        HumanoidCharacterProfile profile,
        AntagSpecifierPrototype definition)
    {
        foreach (var role in definition.PrefRoles)
        {
            if (!profile.AntagPreferences.Contains(role))
                continue;

            // Session bans and playtime are checked before pre-selection. Passing null here
            // intentionally checks only profile-specific requirements such as species, age,
            // and traits for the exact character that will spawn.
            if (JobRequirements.TryRequirementsMet(
                    _role.GetRoleRequirements(role),
                    session,
                    null,
                    out _,
                    EntityManager,
                    _prototypeManager,
                    profile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the antag specifier prototypes this session has been preselected for.
    /// </summary>
    [PublicAPI]
    public HashSet<ProtoId<AntagSpecifierPrototype>> GetPreSelectedAntagSpecifiers(ICommonSession session)
    {
        var result = new HashSet<ProtoId<AntagSpecifierPrototype>>();
        var query = QueryAllRules();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            if (HasComp<EndedGameRuleComponent>(uid))
                continue;

            foreach (var antag in comp.Antags)
            {
                if (comp.PreSelectedSessions.TryGetValue(antag, out var set) && set.Contains(session))
                    result.Add(antag);
            }
        }

        return result;
    }
}
