namespace Content.Server._Starlight.Pollen.Components;

/// <summary>
/// Marks a floating "advanced pollen" cloud spawned by the Floral T3 ability.
/// Every second it checks CheckRange for a Diona, then a pollen-allergic
/// person, then anyone with a mind - first match wins, each gated by its own
/// independent chance. Despawns via TimedDespawnComponent.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenAdvancedPollenComponent : Component
{
    /// <summary>Radius, in tiles, this cloud scans each check.</summary>
    [DataField]
    public float CheckRange = 2f;

    /// <summary>Chance to buff a nearby Diona, once one is found.</summary>
    [DataField]
    public float DionaChance = 0.2f;

    /// <summary>Chance to make a nearby pollen-sensitive person sneeze, once one is found.</summary>
    [DataField]
    public float AllergicChance = 0.5f;

    /// <summary>Chance to show the flavor popup to a nearby minded entity, once one is found.</summary>
    [DataField]
    public float MindChance = 0.1f;

    /// <summary>Next time this cloud checks CheckRange for an interaction.</summary>
    [AutoPausedField]
    public TimeSpan NextCheck;

    /// <summary>Brute damage healed if the Diona-buff roll picks healing.</summary>
    [DataField]
    public float HealBrute = 0.5f;

    /// <summary>Burn damage healed if the Diona-buff roll picks healing.</summary>
    [DataField]
    public float HealBurn = 0.2f;

    /// <summary>Walk speed multiplier applied if the roll picks the speed buff.</summary>
    [DataField]
    public float SpeedWalkModifier = 1.1f;

    /// <summary>Sprint speed multiplier applied if the roll picks the speed buff.</summary>
    [DataField]
    public float SpeedSprintModifier = 1.1f;

    /// <summary>How long the speed buff lasts once granted.</summary>
    [DataField]
    public TimeSpan SpeedBuffDuration = TimeSpan.FromSeconds(5);
}
