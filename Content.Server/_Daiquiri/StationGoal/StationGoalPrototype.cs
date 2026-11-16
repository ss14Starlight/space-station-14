using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.StationGoal;

[Prototype]
public sealed partial class StationGoalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Text { get; set; } = string.Empty;

    [DataField]
    public int? MinPlayers;

    [DataField]
    public int? MaxPlayers;

    /// <summary>
    ///     Items the goal may require. They are spawned next to the receiving fax machine at round start.
    /// </summary>
    [DataField]
    public List<EntProtoId> Spawns = new();
}
