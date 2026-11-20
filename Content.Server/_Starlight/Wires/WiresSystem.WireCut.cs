// ReSharper disable once CheckNamespace
namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    /// <summary>
    ///     Cuts a wire without a tool interaction.
    /// </summary>
    /// <param name="ent">The entity that owns the wire.</param>
    /// <param name="wire">The wire to cut.</param>
    /// <param name="user">The entity responsible for the cut, or null for a world-driven cut.</param>
    /// <returns>True if the wire was cut, false if its action refused the cut.</returns>
    public bool TryCutWire(Entity<WiresComponent> ent, Wire wire, EntityUid? user = null)
    {
        if (wire.IsCut || wire.Owner != ent.Owner)
            return false;

        if (wire.Action != null && !wire.Action.Cut(user ?? EntityUid.Invalid, wire))
            return false;

        wire.IsCut = true;
        wire.Action?.Update(wire);
        UpdateUserInterface(ent, ent.Comp);
        return true;
    }
}
