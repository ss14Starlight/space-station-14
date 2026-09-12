using Robust.Shared.GameStates;

namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Tutorial hydroponics tray: forces planted seeds to harvest-ready and tracks harvests for sensors.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TutorialHydroTrayComponent : Component
{
    /// <summary>
    /// True after a player successfully harvests this tray during the tutorial.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Harvested;

    /// <summary>
    /// Set before PlantHolder harvest handling; cleared after.
    /// </summary>
    [ViewVariables]
    public bool AwaitingHarvestResult;
}
