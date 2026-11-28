using Robust.Shared.Prototypes;

namespace Content.Shared.Spreader;

/// <summary>
/// Adds this node group to <see cref="Content.Server.Spreader.SpreaderSystem"/> for tick updates.
/// </summary>
[Prototype]
public sealed partial class EdgeSpreaderPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = string.Empty;
    [DataField(required:true)] public int UpdatesPerSecond;

    /// <summary>
    /// If true, this spreader can't spread onto spaced tiles like lattice.
    /// </summary>
    [DataField]
    public bool PreventSpreadOnSpaced = true;

    /// <summary>
    /// Starlight
    /// If true, this spreader will ingore doors and will spread under doors.
    /// </summary>
    [DataField]
    public bool IngoreDoors = false;
}
