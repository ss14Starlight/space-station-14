using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Roles;

/// <summary>
/// Networks <see cref="MindRoleComponent.JobPrototype"/> for replays. Live, minds only reach their owner.
/// </summary>
/// <remarks>
/// MindAddJobRole can reassign an existing job without dirtying it; replays keep the old job in that case.
/// Delete this if upstream adds its own state for MindRoleComponent.
/// </remarks>
public sealed partial class MindRoleJobNetworkingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindRoleComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<MindRoleComponent, ComponentHandleState>(OnHandleState);
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
