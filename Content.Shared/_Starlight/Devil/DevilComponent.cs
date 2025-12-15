using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight.Devil;
using Content.Shared.Dataset;

namespace Content.Shared._Starlight.Devil;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class DevilComponent : Component
{
    [DataField]
    public List<ProtoId<EntityPrototype>> BaseActions = new()
    {
        "ActionSummonDemonicContract"
    };

    
    [DataField]
    public List<ProtoId<DamnationPrototype>> AvailableDamnations = new()
    {
        "Soul",
        "Pacifism",
        "Blindness",
        "SpaceImmunity",
        "Credits"
    };

    // todo make actual devil names
    public List<ProtoId<LocalizedDatasetPrototype>> NameSegments = new()
    {
        "NamesDragon",
        "NamesDragonTitle"
    };

    public LocId NameFormat = "name-format-dragon";

    [AutoNetworkedField, ViewVariables]
    public string TrueName = "Hellish McEvil";
}