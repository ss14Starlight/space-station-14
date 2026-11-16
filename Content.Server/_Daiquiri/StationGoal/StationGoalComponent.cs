using Robust.Shared.Prototypes;

namespace Content.Server._Daiquiri.StationGoal;

/// <summary>
///     If attached to a station prototype, sends the station a random goal from the list on round start.
/// </summary>
[RegisterComponent]
public sealed partial class StationGoalComponent : Component
{
    [DataField]
    public List<ProtoId<StationGoalPrototype>> Goals = new();
}
