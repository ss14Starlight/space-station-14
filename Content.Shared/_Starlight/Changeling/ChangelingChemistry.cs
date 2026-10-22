using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Changeling;

public static class ChangelingChemistry
{
    public const string Solution = "changeling-sting";
    public const int Capacity = 30;
    public const int ReagentCapacity = 30;
    public const float RegenerationInterval = 10;
    public const float RegenerationCost = 0.5f;
    public const float StingCost = 10;
    public const float MinTemperature = 100;
    public const float MaxTemperature = 1000;
    public const float TemperatureRate = 20;

    public static readonly ProtoId<ReagentPrototype>[] BasicReagents =
    [
        "Aluminium", "Carbon", "Chlorine", "Copper", "Ethanol", "Fluorine",
        "Hydrogen", "Iodine", "Iron", "Lithium", "Mercury", "Nitrogen", "Oxygen",
        "Phosphorus", "Potassium", "Radium", "Silicon", "Sodium", "Sugar", "Sulfur", "Water",
    ];

    public static readonly ProtoId<ReagentPrototype>[] EvolvedReagents =
    [
        "Nocturine", "Lexorin", "MuteToxin", "ChloralHydrate", "Pax", "UnstableMutagen",
    ];
}

[Serializable, NetSerializable]
public enum ChangelingChemistryUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class ChangelingChemistryUiState(
    Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> reagents,
    Solution mixture,
    float chemicals,
    float targetTemperature,
    bool regenerating,
    int dose) : BoundUserInterfaceState
{
    public readonly Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = reagents;
    public readonly Solution Mixture = mixture;
    public readonly float Chemicals = chemicals;
    public readonly float TargetTemperature = targetTemperature;
    public readonly bool Regenerating = regenerating;
    public readonly int Dose = dose;
}

[Serializable, NetSerializable]
public sealed class ChangelingAddReagentMessage(ProtoId<ReagentPrototype> reagent, int amount) : BoundUserInterfaceMessage
{
    public readonly ProtoId<ReagentPrototype> Reagent = reagent;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class ChangelingClearMixtureMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ChangelingSetTemperatureMessage(float temperature) : BoundUserInterfaceMessage
{
    public readonly float Temperature = temperature;
}

[Serializable, NetSerializable]
public sealed class ChangelingSetRegenerationMessage(bool enabled) : BoundUserInterfaceMessage
{
    public readonly bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class ChangelingSetStingDoseMessage(int dose) : BoundUserInterfaceMessage
{
    public readonly int Dose = dose;
}
