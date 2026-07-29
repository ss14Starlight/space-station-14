using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Trinary.Components
{
    [Serializable, NetSerializable]
    public enum GasFilterUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class GasFilterBoundUserInterfaceState : BoundUserInterfaceState
    {
        public string FilterLabel { get; }
        public float TransferRate { get; }
        public bool Enabled { get; }
        public HashSet<Gas> FilteredGases { get; set; } // Starlight: Multiple gases


        public GasFilterBoundUserInterfaceState(string filterLabel, float transferRate, bool enabled, HashSet<Gas> filteredGases /*Starlight*/)
        {
            FilterLabel = filterLabel;
            TransferRate = transferRate;
            Enabled = enabled;
            FilteredGases = filteredGases; // Starlight: Multiple gases
        }
    }

    [Serializable, NetSerializable]
    public sealed class GasFilterToggleStatusMessage : BoundUserInterfaceMessage
    {
        public bool Enabled { get; }

        public GasFilterToggleStatusMessage(bool enabled)
        {
            Enabled = enabled;
        }
    }

    [Serializable, NetSerializable]
    public sealed class GasFilterChangeRateMessage : BoundUserInterfaceMessage
    {
        public float Rate { get; }

        public GasFilterChangeRateMessage(float rate)
        {
            Rate = rate;
        }
    }

    [Serializable, NetSerializable]
    public sealed class GasFilterSelectGasMessage(HashSet<Gas> gases) : BoundUserInterfaceMessage // Starlight: Multiple gases
    {
        public readonly HashSet<Gas> Gases = gases; // Starlight
    }
}
