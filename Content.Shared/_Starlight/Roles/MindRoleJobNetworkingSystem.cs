using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Roles;

/// <summary>
/// Networks <see cref="MindRoleComponent.JobPrototype"/> for replays. Live, minds only reach their owner.
/// </summary>
/// <remarks>
/// Robust skips state for prototype components that were never dirtied, and the job is set right after the role
/// spawns without dirtying it, so roles are dirtied here whenever one is added. Job reassignment on an existing role is
/// dirtied in SharedRoleSystem.MindAddJobRole.
/// Delete this if upstream adds its own state for MindRoleComponent.
/// </remarks>
public sealed partial class MindRoleJobNetworkingSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindRoleComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<MindRoleComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private void OnRoleAdded(RoleAddedEvent args)
    {
        if (!_net.IsServer)
            return;

        foreach (var role in args.Mind.MindRoleContainer.ContainedEntities)
        {
            if (TryComp<MindRoleComponent>(role, out var comp) && comp.JobPrototype != null)
                Dirty(role, comp);
        }
    }

    private void OnGetState(Entity<MindRoleComponent> ent, ref ComponentGetState args) =>
        args.State = new MindRoleJobComponentState { JobPrototype = ent.Comp.JobPrototype };

    private void OnHandleState(Entity<MindRoleComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not MindRoleJobComponentState state)
            return;

        ent.Comp.JobPrototype = state.JobPrototype;
    }
}

[Serializable, NetSerializable]
public sealed class MindRoleJobComponentState : ComponentState
{
    public ProtoId<JobPrototype>? JobPrototype;
}
