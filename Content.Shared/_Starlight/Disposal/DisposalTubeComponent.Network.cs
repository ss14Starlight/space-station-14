// ReSharper disable once CheckNamespace
namespace Content.Shared.Disposal.Tube;

public sealed partial class DisposalTubeComponent
{
    /// <summary>
    /// Tubes only connect to neighbours of the same network. Disposal pipes and transit tubes
    /// share the same tube and holder logic but must never join each other.
    /// </summary>
    [DataField]
    public string Network = "Disposal";

    /// <summary>
    /// Exits, before rotation, that join another network than <see cref="Network"/>.
    /// A transit tube ramp has one end on the floor network and the other under or over it.
    /// </summary>
    [DataField]
    public Dictionary<Direction, string>? ExitNetworks;
}

public sealed partial class DisposalTubeSystem
{
    /// <summary>
    /// The network a tube joins through its exit in a world direction.
    /// </summary>
    public string GetExitNetwork(Entity<DisposalTubeComponent> ent, Direction direction)
    {
        if (ent.Comp.ExitNetworks is not { } networks)
            return ent.Comp.Network;

        var local = new Angle(direction.ToAngle() - Transform(ent).LocalRotation).GetDir();
        return networks.GetValueOrDefault(local, ent.Comp.Network);
    }
}
