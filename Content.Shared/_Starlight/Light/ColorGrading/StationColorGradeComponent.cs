using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Light.ColorGrading;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationColorGradeComponent : Component
{
    /// <summary>
    /// Grade of the station's alert level, null when the level has none.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ColorGradePrototype>? AlertGrade;
}
