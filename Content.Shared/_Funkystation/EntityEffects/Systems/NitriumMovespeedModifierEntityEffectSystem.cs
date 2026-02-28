using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Funkystation.EntityEffects.Effects.StatusEffects;

/// <summary>
/// Special movement speed modifier for Nitrium gas:
/// Every metabolism tick with Nitrium present, SETS the remaining duration to exactly 6 seconds.
/// Uses existing MovementSpeedModifierComponent and MovementModStatusSystem.
/// </summary>
public sealed partial class NitriumMovementSpeedModifierSystem : EntityEffectSystem<MovementSpeedModifierComponent, NitriumMovementSpeedModifier>
{
    [Dependency] private readonly StatusEffectNew.StatusEffectsSystem _status = default!;
    [Dependency] private readonly MovementModStatusSystem _movementModStatus = default!;

    protected override void Effect(Entity<MovementSpeedModifierComponent> entity, ref EntityEffectEvent<NitriumMovementSpeedModifier> args)
    {
        var effect = args.Effect;
        var proto = effect.EffectProto;

        if (_status.TrySetStatusEffectDuration(entity, proto, out var statusEnt, TimeSpan.FromSeconds(6f)))
        {
            _movementModStatus.TryUpdateMovementStatus(
                entity,
                statusEnt.Value,
                effect.WalkSpeedModifier,
                effect.SprintSpeedModifier
            );
        }
    }
}

/// <summary>
/// Reagent effect data for Nitrium's movement speed boost
/// </summary>
public sealed partial class NitriumMovementSpeedModifier : BaseStatusEntityEffect<NitriumMovementSpeedModifier>
{
    /// <summary>
    /// How much the entities' walk speed is multiplied by.
    /// </summary>
    [DataField]
    public float WalkSpeedModifier = 1f;

    /// <summary>
    /// How much the entities' run speed is multiplied by.
    /// </summary>
    [DataField]
    public float SprintSpeedModifier = 1f;

    /// <summary>
    /// Movement speed modifier prototype we're adding.
    /// </summary>
    [DataField]
    public EntProtoId EffectProto = MovementModStatusSystem.ReagentSpeed;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("entity-effect-guidebook-movespeed-modifier",
            ("chance", Probability),
            ("sprintspeed", SprintSpeedModifier),
            ("time", 6));
}
