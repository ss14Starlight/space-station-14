using System.Linq;
using Content.Shared._Moffstation.Roles;
using Content.Shared.Mind;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public abstract partial class SharedRoleSystem
{
    private ProtoId<RoleTypePrototype> GetJobRoleType(MindComponent mind)
    {
        foreach (var uid in mind.MindRoleContainer.ContainedEntities)
        {
            if (TryComp<MindRoleComponent>(uid, out var role)
                && role.JobPrototype is { } jobId
                && !string.IsNullOrEmpty(jobId.Id)
                && _prototypes.TryIndex(jobId, out var job))
                return job.RoleType;
        }

        return "Neutral";
    }

    /// <summary>
    /// Returns the active role's type, displayed name, and faction color, or the job's default role type.
    /// </summary>
    private (ProtoId<RoleTypePrototype>, LocId?, Color?) GetRoleTypeByTime(MindComponent mind)
    {
        var role = GetRoleCompByTime(mind)?.Comp;
        var roleType = role?.RoleType ?? GetJobRoleType(mind);
        var subtype = role?.CharacterMenuName ?? role?.Subtype;
        var color = role?.SubtypeColor;

        if (role?.AntagPrototype is not { } antag)
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
}
