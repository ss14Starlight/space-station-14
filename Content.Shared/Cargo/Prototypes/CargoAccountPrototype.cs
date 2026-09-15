using Content.Shared._Starlight.Cargo.TamperSeal.Components;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

#region Starlight
using Content.Shared.Access;
#endregion

namespace Content.Shared.Cargo.Prototypes;

/// <summary>
/// This is a prototype for a single account that stores money on StationBankAccountComponent
/// </summary>
[Prototype]
public sealed partial class CargoAccountPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Full IC name of the account.
    /// </summary>
    [DataField]
    public LocId Name;

    /// <summary>
    /// A shortened code used to refer to the account in UIs
    /// </summary>
    [DataField]
    public LocId Code;

    /// <summary>
    /// Color corresponding to the account.
    /// </summary>
    [DataField]
    public Color Color;

    /// <summary>
    /// Channel used for announcing transactions.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> RadioChannel;

    /// <summary>
    /// Paper prototype used for acquisition slips.
    /// </summary>
    [DataField]
    public EntProtoId AcquisitionSlip;

    #region Starlight
    /// <summary>
    /// The name of the recipient to use on tamper seal examination.
    /// </summary>
    [DataField] public LocId TamperSealName;

    /// <summary>
    /// The color to use for tamper seals.
    /// </summary>
    [DataField] public Color TamperSealColor;

    /// <summary>
    /// The access levels that can unseal orders that are bound to this account.
    /// </summary>
    [DataField]
    public List<TamperSealAccessPattern> TamperSealAccesses = new();
    #endregion
}
