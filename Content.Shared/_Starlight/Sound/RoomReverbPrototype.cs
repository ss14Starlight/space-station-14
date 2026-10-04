using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Starlight.Sound;

[Prototype]
public sealed partial class RoomReverbPrototype : IPrototype
{
    public static readonly ProtoId<RoomReverbPrototype> Default = "Default";

    [IdDataField] public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public List<RoomReverbTier> Tiers = new();

    [DataField]
    public float Amount = 0.5f;

    public ProtoId<AudioPresetPrototype> PresetFor(int roomSize)
    {
        foreach (var tier in Tiers)
        {
            if (roomSize <= tier.MaxTiles)
                return tier.Preset;
        }

        return Tiers[^1].Preset;
    }
}

[DataDefinition]
public partial record struct RoomReverbTier
{
    [DataField(required: true)]
    public int MaxTiles;

    [DataField(required: true)]
    public ProtoId<AudioPresetPrototype> Preset;
}
