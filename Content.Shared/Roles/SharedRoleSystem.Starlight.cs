using System.Linq;
using Content.Shared._Moffstation.Roles;
using Content.Shared.Mind;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public abstract partial class SharedRoleSystem
{
    private (ProtoId<RoleTypePrototype>, LocId?, Color?) GetJobRoleType(MindComponent mind)
    {
        foreach (var uid in mind.MindRoleContainer.ContainedEntities)
        {
            if (TryComp<MindRoleComponent>(uid, out var role)
                && role.JobPrototype is { } jobId
                && !string.IsNullOrEmpty(jobId.Id)
                && _prototypes.TryIndex(jobId, out var job))
            {
                var subtype = job.RoleSubtype;
                if (subtype == null && _prototypes.TryIndex(job.RoleType, out var roleType))
                    subtype = roleType.Faction;

                return (job.RoleType, subtype, null);
            }
        }

        return ("Neutral", "role-subtype-contractor", null);
    }

    /// <summary>
    /// Returns the active role's type, displayed name, and faction color, or the job's default role type.
    /// </summary>
    private (ProtoId<RoleTypePrototype>, LocId?, Color?) GetRoleTypeByTime(MindComponent mind)
    {
        var role = GetRoleCompByTime(mind)?.Comp;

        // we have to care about generic categories for roleban stuff, but we should only display them if there's literally nothing else to display
        if (role != null && IsGenericRole(role))
        {
            var latestRoleType = role.RoleType;
            foreach (var uid in mind.MindRoleContainer.ContainedEntities)
            {
                if (TryComp<MindRoleComponent>(uid, out var candidate)
                    && candidate.RoleType == latestRoleType
                    && !IsGenericRole(candidate))
                    role = candidate;
            }
        }

        if (role?.RoleType is not { } roleType)
            return GetJobRoleType(mind);

        var subtype = role.CharacterMenuName ?? role.Subtype;
        if (subtype == null && _prototypes.TryIndex(roleType, out var rolePrototype))
            subtype = rolePrototype.Faction;
        var color = role.SubtypeColor;

        if (role.AntagPrototype is not { } antag)
            return (roleType, subtype, color);

        if (_prototypes.Resolve(antag, out var prototype))
            subtype = role.CharacterMenuName ?? prototype.Name;

        foreach (var category in _prototypes.EnumeratePrototypes<AntagCategoryPrototype>()
                    .OrderBy(c => c.Default).ThenBy(c => c.Priority).ThenBy(c => c.Name))
        {
            if (!category.Antags.Contains(antag))
                continue;

            return (roleType,
                subtype,
                category.FactionColor ?? (category.KeepIndividualColors ? color : category.AccentColor));
        }

        return (roleType, subtype, color);
    }

    private bool IsGenericRole(MindRoleComponent role) => role.CharacterMenuName == null
            && role.AntagPrototype is { } antag
            && _prototypes.Resolve(antag, out var prototype)
            && prototype.IsGeneric;
}
