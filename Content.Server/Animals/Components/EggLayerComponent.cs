using Content.Server.Animals.Systems;
using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

#region Starlight
using Content.Server.Genetics;
using Content.Shared.Genetics;
#endregion Starlight

namespace Content.Server.Animals.Components;

/// <summary>
///     This component handles animals which lay eggs (or some other item) on a timer, using up hunger to do so.
///     It also grants an action to players who are controlling these entities, allowing them to do it manually.
/// </summary>

[RegisterComponent, Access(typeof(EggLayerSystem), typeof(GeneticsSystem)), AutoGenerateComponentPause] // Starlight-edit - add GeneticsSystem access
[GeneticComponent(4, 6)] // Starlight
public sealed partial class EggLayerComponent : Component
{
    /// <summary>
    ///     The item that gets laid/spawned, retrieved from animal prototype.
    /// </summary>
    [DataField(required: true)]
    // Starlight start
    [GeneticsEnumBasedVariable(nameof(GetEggSpawnKey), nameof(SetEggSpawnKey))]
    [GeneticsEnumEntry(2, 2, "FoodEgg")]
    [GeneticsEnumEntry(4, 2, "FoodEggChickenFertilized")]
    [GeneticsEnumEntry(4, 2, "FoodEggDuckFertilized")]
    [GeneticsEnumEntry(4, 0, "FoodEggplant")]
    [GeneticsEnumEntry(7, 0, "FoodMealEggsbenedict")]
    [GeneticsEnumEntry(5, 1, "FoodEggCompyFertilized")]
    [GeneticsEnumEntry(5, 2, "EggSpider")]
    // Starlight end
    public List<EntitySpawnEntry> EggSpawn = new();

    // Starlight start - these functions are effectively getters and setters for EggSpawn
    // for the Genetics system
    /// <summary>
    /// Returns the string key representing the current EggSpawn value,
    /// or null if it doesn't match any known entry.
    /// </summary>
    public string? GetEggSpawnKey()
    {
        return EggSpawn.Count > 0 ? EggSpawn[0].PrototypeId?.Id : null;
    }

    /// <summary>
    /// Sets EggSpawn from a string key (prototype ID), or resets to
    /// empty if null.
    /// </summary>
    public void SetEggSpawnKey(string? key)
    {
        EggSpawn = key != null
            ? new List<EntitySpawnEntry> { new() { PrototypeId = key } }
            : new List<EntitySpawnEntry>();
    }
    // Starlight end

    /// <summary>
    ///     Player action.
    /// </summary>
    [DataField]
    public EntProtoId EggLayAction = "ActionAnimalLayEgg";

    [DataField]
    public SoundSpecifier EggLaySound = new SoundPathSpecifier("/Audio/Effects/pop.ogg");

    /// <summary>
    ///     Minimum cooldown used for the automatic egg laying.
    /// </summary>
    [DataField]
    public float EggLayCooldownMin = 60f;

    /// <summary>
    ///     Maximum cooldown used for the automatic egg laying.
    /// </summary>
    [DataField]
    public float EggLayCooldownMax = 120f;

    /// <summary>
    ///     The amount of nutrient consumed on update.
    /// </summary>
    [DataField]
    public float HungerUsage = 30f; // Starlight edit 60f -> 30f

    [DataField] public EntityUid? Action;

    /// <summary>
    ///     When to next try to produce.
    /// </summary>
    [DataField, AutoPausedField]
    public TimeSpan NextGrowth = TimeSpan.Zero;
}
