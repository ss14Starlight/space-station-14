using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Stains.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StainableComponent : Component
{
    [DataField]
    public string SolutionName = "stain";

    // Moff start - Reduce stain volume
    // Reduce stain volume so its not messing with puddles so much
    // right now the specific volume doesnt matter that much, if that changes we can tweak it.
    [DataField]
    public FixedPoint2 MaxStainVolume = FixedPoint2.New(1);

    [DataField]
    public FixedPoint2 SpillTransferAmount = 0.1f;
    // Moff end

    [DataField]
    public float WringDoAfterDuration = 15f;

    [DataField]
    public Dictionary<string, List<PrototypeLayerData>> ClothingVisuals = new();

    [DataField]
    public Dictionary<string, List<PrototypeLayerData>> ItemVisuals = new();

    [DataField]
    public List<PrototypeLayerData> IconVisuals = new();

    [ViewVariables]
    public HashSet<int> RevealedLayers = new();

    // Moff start - Stains not guaranteed
    [DataField]
    public float StainChance = 0.5f;
    // Moff end
}

[Serializable, NetSerializable]
public sealed partial class WringStainDoAfterEvent : SimpleDoAfterEvent;
