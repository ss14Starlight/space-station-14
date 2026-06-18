using Content.Shared.Atmos;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.CosmicCult.Components;

[RegisterComponent]
public sealed partial class CosmicSpireComponent : Component
{

    [DataField]
    public bool Enabled;

    [DataField]
    public float DrainRate = 500;

    [DataField]
    public float DrainThreshHold = 3250;

    [DataField]
    public int MotesCreated = 0;

    [DataField]
    public int MotesCap = 7;

    [DataField]
    public int CapEntropyBonus = 5;

    [DataField]
    public HashSet<Gas> DrainGases = new()
    {
        Gas.Oxygen,
        Gas.Nitrogen,
        Gas.CarbonDioxide,
        Gas.WaterVapor,
        Gas.Ammonia,
        Gas.NitrousOxide,
    };

    [DataField]
    public GasMixture Storage = new();

    [DataField]
    public EntProtoId EntropyMote = "MaterialCosmicCultEntropy1";

    [DataField]
    public EntProtoId EntropyMoteStack = "MaterialCosmicCultEntropy5";

    [DataField]
    public EntProtoId SpawnVFX = "CosmicGenericVFX";

    [DataField]
    public SoundSpecifier DespawnSFX = new SoundPathSpecifier("/Audio/_Starlight/CosmicCult/effigy_supercritical.ogg");
}

[Serializable, NetSerializable]
public enum SpireVisuals : byte
{
    Status,
}

[Serializable, NetSerializable]
public enum SpireStatus : byte
{
    Off,
    On,
}
