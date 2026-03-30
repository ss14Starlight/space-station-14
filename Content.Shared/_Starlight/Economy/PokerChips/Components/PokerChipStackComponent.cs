using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Economy.PokerChips.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PokerChipStackComponent : Component
{
    // TODO: find sounds for these.
    [DataField] public SoundSpecifier InsertSound = new SoundCollectionSpecifier();
    [DataField] public SoundSpecifier RemoveSound = new SoundCollectionSpecifier();
    [DataField] public string ContainerId = "poker-chip-stack-container";
    [DataField] public float YOffset = 0.02f;
    [DataField] public int[] SplitAmounts = [1, 5, 10, 20, 30, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000];
    [DataField] public string ExaminedLocId = "poker-chip-stack-examined";
    [DataField] public string ExaminedValueLocId = "poker-chip-value";
    [DataField] public string DrawVerbLocId = "poker-chip-stack-draw-verb-text";
    [DataField] public string DrawVerbIconName = "eject";
    [DataField] public string JoinVerbLocId = "poker-chip-stack-join-verb-text";
    [DataField] public string JoinVerbIconName = "pickup";
    [DataField] public string SplitCountVerbLocId = "poker-chip-stack-split-count-verb-text";
    [DataField] public string SplitValueVerbLocId = "poker-chip-stack-split-value-verb-text";
    [ViewVariables, AutoNetworkedField] public Stack<NetEntity> Chips = [];
    [ViewVariables] public Container Container;
    [ViewVariables] public int ChipCount => Chips.Count;
    [ViewVariables] public HashSet<string> SpriteLayersAdded = [];
}

[Serializable, NetSerializable]
public enum PokerChipStackVisuals : byte
{
    Chips,
}
