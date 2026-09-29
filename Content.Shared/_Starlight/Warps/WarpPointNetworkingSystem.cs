using Content.Shared.Pinpointer;
using Content.Shared.Warps;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Warps;

/// <summary>
/// Networks <see cref="WarpPointComponent.Location"/> so clients and replays get map-defined warp names.
/// </summary>
/// <remarks>
/// Delete this if upstream adds its own state for WarpPointComponent.
/// </remarks>
public sealed partial class WarpPointNetworkingSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarpPointComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<WarpPointComponent, ComponentHandleState>(OnHandleState);

        // NavMapSystem renames these without dirtying them. Handler order doesn't matter; state is read at send time.
        SubscribeLocalEvent<WarpPointComponent, NavMapBeaconConfigureBuiMessage>(OnBeaconConfigured);
    }

    private void OnGetState(Entity<WarpPointComponent> ent, ref ComponentGetState args) =>
        args.State = new WarpPointComponentState { Location = ent.Comp.Location };

    private void OnHandleState(Entity<WarpPointComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not WarpPointComponentState state)
            return;

        ent.Comp.Location = state.Location;
    }

    private void OnBeaconConfigured(Entity<WarpPointComponent> ent, ref NavMapBeaconConfigureBuiMessage args)
    {
        if (_net.IsServer)
            Dirty(ent);
    }
}

[Serializable, NetSerializable]
public sealed class WarpPointComponentState : ComponentState
{
    public string? Location;
}
