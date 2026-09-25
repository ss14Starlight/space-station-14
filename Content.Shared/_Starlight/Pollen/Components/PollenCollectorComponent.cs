using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Pollen.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PollenCollectorComponent : Component
{
    /// <summary>
    /// Pollen this entity can still absorb, keyed by pollen id (= produce SeedId).
    /// false = not collected yet, true = collected. Filled once on MapInit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, bool> Pollen = new();

    /// <summary>How many plants get rolled into <see cref="Pollen"/> on spawn.</summary>
    [DataField]
    public int PlantCount = 5;

    /// <summary>Points granted per collected plant.</summary>
    [DataField]
    public int PointsPerPlant = 2;

    /// <summary>How many entries in <see cref="Pollen"/> are true.</summary>
    [DataField, AutoNetworkedField]
    public int Collected;

    [DataField]
    public float PollenRange = 1f;

    [DataField]
    public float InteractionChance = 1f;

    public TimeSpan NextCollection;

    public bool ObjectiveGranted;
}
