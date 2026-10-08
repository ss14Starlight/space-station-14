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

    public RoomReverbTier TierFor(int roomSize)
    {
        foreach (var tier in Tiers)
        {
            if (roomSize <= tier.MaxTiles)
                return tier;
        }

        return Tiers[^1];
    }
}

[DataDefinition]
public partial record struct RoomReverbTier
{
    [DataField(required: true)]
    public int MaxTiles;

    [DataField(required: true)]
    public ProtoId<AudioPresetPrototype> Preset;

    /// <summary>
    /// Multiplies the preset's decay time, to shorten long tails without picking a different character.
    /// </summary>
    [DataField]
    public float DecayScale = 1f;

    /// <summary>
    /// Multiplies the preset's reflection and late reverb delays, so the walls answer audibly later.
    /// </summary>
    [DataField]
    public float DelayScale = 1f;
}
