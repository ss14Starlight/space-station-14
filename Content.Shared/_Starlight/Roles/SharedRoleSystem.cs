using System.Diagnostics.CodeAnalysis;
using Content.Shared.Mind;
using Content.Shared.Prototypes;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Roles;

public abstract partial class SharedRoleSystem
{
    [Dependency] private IComponentFactory _factory = null!;

    /// <summary>
    /// Finds the first mind role of a specific prototype on a mind entity.
    /// </summary>
    /// <param name="mind">The mind entity.</param>
    /// <param name="mindRolePrototype">The prototype to find.</param>
    /// <param name="role">The resulting output role entity.</param>
    /// <returns><see langword="true"/> if the role is found.</returns>
    public bool MindHasRole(Entity<MindComponent?> mind, EntProtoId mindRolePrototype,
        [NotNullWhen(true)] out Entity<MindRoleComponent>? role)
    {
        role = null;
        if (!Resolve(mind.Owner, ref mind.Comp))
            return false;

        if (!_prototypes.TryIndex(mindRolePrototype, out var prototype))
            throw new ArgumentException($"Entity prototype {mindRolePrototype.Id} does not exist.",
                nameof(mindRolePrototype));
        if (!prototype.HasComponent<MindRoleComponent>(_factory))
            throw new ArgumentException($"Entity prototype {mindRolePrototype.Id} is not a mind role.",
                nameof(mindRolePrototype));

        foreach (var roleEnt in mind.Comp.MindRoleContainer.ContainedEntities)
        {
            if (!TryComp<MindRoleComponent>(roleEnt, out var comp))
                throw new Exception(
                    $"Entity {ToPrettyString(mind)} has entity {ToPrettyString(roleEnt)} as a mind role, which does not have {nameof(MindRoleComponent)}.");

            if (MetaData(roleEnt).EntityPrototype != prototype) continue;
            role = (roleEnt, comp);
            return true;
        }

        return false;
    }
}
