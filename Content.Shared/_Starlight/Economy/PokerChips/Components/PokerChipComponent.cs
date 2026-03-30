using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Economy.PokerChips.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PokerChipComponent : Component
{
    // Values are increased from what they would normally be to compensate for the current econ.
    [DataField, AutoNetworkedField] public Dictionary<PokerChipValue, Dictionary<int, Color>> ChipColorThresholds = new()
    {
        {
            PokerChipValue.Credits , new Dictionary<int, Color>
            {
                {0, Color.White},
                {50, Color.Red},
                {100, Color.FromHex("#2c3ad4")},
                {1000, Color.FromHex("#07ba0d")},
                {10000, Color.FromHex("#121212")}
            }
        },
        {
            PokerChipValue.Spesos , new Dictionary<int, Color>
            {
                {0, Color.White},
                {10, Color.Red},
                {50, Color.FromHex("#2c3ad4")},
                {500, Color.FromHex("#07ba0d")},
                {1000, Color.FromHex("#121212")}
            }
        }
    };
    [DataField, AutoNetworkedField] public Dictionary<PokerChipValue, Dictionary<int, Color>> DecalColorThresholds = new()
    {
        {
            PokerChipValue.Credits , new Dictionary<int, Color>
            {
                {0, Color.FromHex("#34a1eb")},
                {50, Color.White},
                {10000, Color.FromHex("#e4db28")}
            }
        },
        {
            PokerChipValue.Spesos , new Dictionary<int, Color>
            {
                {0, Color.FromHex("#34a1eb")},
                {10, Color.White},
                {10000, Color.FromHex("#e4db28")}
            }
        }
    };
    [DataField, AutoNetworkedField] public Dictionary<PokerChipValue, Dictionary<int, Color>> TypeColorThresholds = new()
    {
        {
            PokerChipValue.Credits , new Dictionary<int, Color>
            {
                {0, Color.FromHex("#34a1eb")},
                {50, Color.White},
                {10000, Color.FromHex("#e4db28")}
            }
        },
        {
            PokerChipValue.Spesos , new Dictionary<int, Color>
            {
                {0, Color.FromHex("#34a1eb")},
                {10, Color.White},
                {10000, Color.FromHex("#e4db28")}
            }
        }
    };

    [DataField] public string ValueLayerKey = "chip-value-layer";
    [DataField] public string ValueStatePrefix = "identifier_";
    [DataField] public string ExaminedLocId = "poker-chip-examined";
    [DataField] public string ExaminedValueLocId = "poker-chip-value";
    [DataField] public EntProtoId StackPrototypeId = "PokerChipStack";
    [DataField, AutoNetworkedField] public PokerChipValue ChipValueType;
    [DataField, AutoNetworkedField] public int ChipValue;
}

public enum PokerChipValue : byte
{
    Credits,
    Spesos
}
