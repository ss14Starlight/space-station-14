using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Arcade.Lancer;

[Prototype("lancerEncounter")]
public sealed partial class LancerEncounterPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public LocId Description = string.Empty;

    [DataField(required: true)]
    public LocId ObjectiveText = string.Empty;

    [DataField(required: true)]
    public LancerObjectiveKind Objective;

    [DataField]
    public int ObjectiveX;

    [DataField]
    public int ObjectiveY;

    [DataField]
    public int HoldTurnsRequired = 3;

    [DataField]
    public int PlayerDeployX = 2;

    [DataField]
    public int PlayerDeployY = 10;

    [DataField]
    public bool HasRelay = true;

    [DataField]
    public int RelayX = 2;

    [DataField]
    public int RelayY = 5;

    [DataField]
    public List<LancerTerrainEntry> Terrains = new();

    [DataField]
    public List<LancerEnemySpawnEntry> Enemies = new();

    [DataField(required: true)]
    public List<LancerNarrativeCheckEntry> NarrativeChecks = new();
}

[DataDefinition]
public sealed partial class LancerTerrainEntry
{
    [DataField(required: true)]
    public int X;

    [DataField(required: true)]
    public int Y;

    [DataField(required: true)]
    public LancerTerrainType Terrain;
}

[DataDefinition]
public sealed partial class LancerEnemySpawnEntry
{
    [DataField(required: true)]
    public LancerUnitKind Kind;

    [DataField(required: true)]
    public int X;

    [DataField(required: true)]
    public int Y;

    [DataField]
    public int Tier;

    [DataField]
    public bool Veteran;

    /// <summary>Optional RSI state override from <c>lancer_units.rsi</c> (e.g. <c>kerberos_grunt</c>).</summary>
    [DataField]
    public string SpriteState = string.Empty;
}

[DataDefinition]
public sealed partial class LancerNarrativeCheckEntry
{
    [DataField(required: true)]
    public LocId Label;

    [DataField(required: true)]
    public LocId Description;

    [DataField]
    public int Modifier;

    [DataField]
    public int Dc = 10;

    [DataField(required: true)]
    public LancerNarrativeBonusKind Bonus;

    [DataField]
    public int BonusValue = 1;
}
