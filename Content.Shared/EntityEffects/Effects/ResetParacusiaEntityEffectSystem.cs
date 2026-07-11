using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

/// <summary>
/// Resets the Paracusia timer on a given entity.
/// The new duration of the timer is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ResetParacusiaEntityEffectSystem : EntityEffectSystem<ParacusiaComponent, ResetParacusia>
{
	[Dependency] private SharedParacusiaSystem _paracusia = default!;

	protected override void Effect(Entity<ParacusiaComponent> entity, ref EntityEffectEvent<ResetParacusia> args)
	{
		var timer = args.Effect.TimerReset * args.Scale;

		_paracusia.ResetParacusiaTimer(entity.AsNullable(), timer);
	}
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ResetParacusia : EntityEffectBase<ResetParacusia>
{
    /// <summary>
    /// The time we set our Paracusia timer to.
    /// </summary>
    [DataField("TimerReset")]
    public TimeSpan TimerReset = TimeSpan.FromSeconds(600);

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys, ILocalizationManager loc) => // Starlight
        loc.GetString("entity-effect-guidebook-reset-narcolepsy", ("chance", Probability));
}
