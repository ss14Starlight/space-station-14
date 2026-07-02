using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Shared.Network;

namespace Content.Server._Starlight.Tidr;

/// <summary>
///     Station-wide shared task board for Tidr (the NanoTask rework).
///     Lives on the station entity. Every Tidr cartridge on the station reads
///     from and writes to this one list, so all crew see the same board.
///
///     Identity model: ID cards gate PERMISSIONS (who may edit/complete/delete/release),
///     player accounts carry MONEY (escrow is withdrawn from and refunded to the player
///     who performed the action, resolved at action time).
/// </summary>
[RegisterComponent]
public sealed partial class TidrBoardComponent : Component
{
    /// <summary>
    ///     The shared list of tasks for the whole station.
    /// </summary>
    [DataField]
    public List<NanoTaskItemAndId> Tasks = new();

    /// <summary>
    ///     Counter for generating unique task IDs across the station.
    /// </summary>
    [DataField]
    public int Counter = 1;

    /// <summary>
    ///     Task id -> the ID card entity that posted it. Gates edit/complete/delete.
    ///     Server-only runtime state, not serialized.
    /// </summary>
    public Dictionary<int, EntityUid> Owners = new();

    /// <summary>
    ///     Task id -> the ID card entity of the Tider who claimed it. Gates release.
    ///     Server-only runtime state, not serialized.
    /// </summary>
    public Dictionary<int, EntityUid> Accepters = new();

    /// <summary>
    ///     Task id -> credits held in escrow for that task. The money has already been
    ///     withdrawn from the poster; on completion it pays the accepter (minus the NT cut),
    ///     on deletion or round end it refunds the poster. Absence of a key on a rewarded,
    ///     completed task means the payout already happened.
    /// </summary>
    public Dictionary<int, int> Escrow = new();

    /// <summary>
    ///     Task id -> the player (account) who funded the escrow, for refunds.
    /// </summary>
    public Dictionary<int, NetUserId> OwnerUsers = new();

    /// <summary>
    ///     Task id -> the player (account) who accepted the task, for payout.
    /// </summary>
    public Dictionary<int, NetUserId> AccepterUsers = new();
}
