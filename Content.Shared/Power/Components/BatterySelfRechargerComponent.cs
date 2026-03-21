using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared._Starlight.Power.EntitySystems;
using JetBrains.Annotations; // Starlight

namespace Content.Shared.Power.Components;

/// <summary>
/// Self-recharging battery.
/// To be used in combination with <see cref="BatteryComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BatterySelfRechargerComponent : Component
{
    /// <summary>
    /// At what rate does the entity automatically recharge? In watts.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables]
    public float AutoRechargeRate;
    
    //Starlight begin
    [ViewVariables(VVAccess.ReadWrite)]
    public float AutoRechargeRateVV
    {
        get => AutoRechargeRate;
        set
        {
            var em = IoCManager.Resolve<IEntityManager>();
            var query = em.EntityQueryEnumerator<BatterySelfRechargerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp != this) continue;
                em.System<BatterySelfRechargerSystem>().UpdateAutoRechargeRate(uid, value, this);
                break;
            }
        }
    }
    //Starlight end

    /// <summary>
    /// How long should the entity stop automatically recharging if a charge is used?
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan AutoRechargePauseTime = TimeSpan.Zero;

    /// <summary>
    /// Do not auto recharge if this timestamp has yet to happen, set for the auto recharge pause system.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField, AutoPausedField, ViewVariables]
    public TimeSpan? NextAutoRecharge = TimeSpan.FromSeconds(0);
}
