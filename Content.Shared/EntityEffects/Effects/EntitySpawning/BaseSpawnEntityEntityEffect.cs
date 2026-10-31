using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// A type of <see cref="EntityEffectBase{T}"/> for effects that spawn entities by prototype.
/// </summary>
/// <typeparam name="T">The entity effect inheriting this BaseEffect</typeparam>
/// <inheritdoc cref="EntityEffect"/>
public abstract partial class BaseSpawnEntityEntityEffect<T> : EntityEffectBase<T> where T : BaseSpawnEntityEntityEffect<T>
{
    /// <summary>
    /// Amount of entities we're spawning
    /// </summary>
    [DataField]
    public int Number = 1;

    /// <summary>
    /// Prototype of the entity we're spawning
    /// </summary>
    // Starlight begin - Allow spawning empty entity.
    [DataField]
    public EntProtoId? Entity;
    // Starlight end

    /// <summary>
    /// Whether this spawning is predicted. Set false to not predict the spawn.
    /// Entities with animations or that have random elements when spawned should set this to false.
    /// </summary>
    [DataField]
    public bool Predicted = true;

    #region Starlight

    /// Component overrides for the spawned entity.
    [DataField] public ComponentRegistry? Overrides;

    #endregion

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) // Starlight
        => loc.GetString("entity-effect-guidebook-spawn-entity",
            ("chance", Probability),
            ("entname", Entity is not null ? IoCManager.Resolve<IPrototypeManager>().Index<EntityPrototype>(Entity).Name : "Unknown"), // Starlight edit
            ("amount", Number));
}
