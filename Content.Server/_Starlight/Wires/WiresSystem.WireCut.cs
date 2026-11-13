namespace Content.Server.Wires;

public sealed partial class WiresSystem
{
    /// <summary>
    ///     Cuts a wire without a tool interaction.
    /// </summary>
    /// <param name="uid">The entity that owns the wire.</param>
    /// <param name="wire">The wire to cut.</param>
    /// <param name="user">The entity responsible for the cut, or null for a world-driven cut.</param>
    /// <param name="wires">The entity's <see cref="WiresComponent"/>, if already resolved.</param>
    /// <returns>True if the wire was cut, false if its action refused the cut.</returns>
    public bool TryCutWire(EntityUid uid, Wire wire, EntityUid? user = null, WiresComponent? wires = null)
    {
        if (!Resolve(uid, ref wires, false) || wire.IsCut || wire.Owner != uid)
            return false;

        // World-driven cuts have no user. EntityUid.Invalid is safe here: the
        // electrocution path simply cannot find status effects on it.
        if (wire.Action != null && !wire.Action.Cut(user ?? EntityUid.Invalid, wire))
            return false;

        wire.IsCut = true;
        wire.Action?.Update(wire);
        UpdateUserInterface(uid, wires);
        return true;
    }
}
