using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Arcade.Lancer;

[Prototype("lancerMission")]
public sealed partial class LancerMissionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public LocId Description = string.Empty;

    /// <summary>Must be cleared before this mission appears unlocked.</summary>
    [DataField]
    public string? UnlocksAfter;

    /// <summary>Mission id unlocked when this mission is cleared.</summary>
    [DataField]
    public string? UnlocksMission;

    [DataField(required: true)]
    public List<ProtoId<LancerEncounterPrototype>> Encounters = new();

    /// <summary>
    /// Optional per-loadout encounter lists. Key = loadout id (e.g. tokugawa).
    /// When present, that loadout uses these fights instead of <see cref="Encounters"/>.
    /// </summary>
    [DataField]
    public Dictionary<string, List<ProtoId<LancerEncounterPrototype>>> LoadoutEncounters = new();
}
