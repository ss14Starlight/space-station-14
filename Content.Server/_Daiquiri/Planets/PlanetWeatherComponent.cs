using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Daiquiri.Planets;

[RegisterComponent]
public sealed partial class PlanetWeatherComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public List<EntProtoId> Weathers = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? Current;

    [ViewVariables]
    public EntityUid? CurrentWeather;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntProtoId? Lightning;

    [ViewVariables(VVAccess.ReadWrite)]
    public float LightningShockDamage;

    [ViewVariables(VVAccess.ReadWrite)]
    public LocId? StartMessage;

    [ViewVariables(VVAccess.ReadWrite)]
    public LocId? EndMessage;

    [ViewVariables(VVAccess.ReadWrite)]
    public Color MessageColor = Color.White;

    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<DamageTypePrototype> DamageType = "Cold";

    [ViewVariables(VVAccess.ReadWrite)]
    public float DamagePerSecond = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public float DamageAccumulator;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Active;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextStorm;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan StormEnd;

    [ViewVariables(VVAccess.ReadWrite)]
    public float SecondsUntilWeather
    {
        get
        {
            var now = IoCManager.Resolve<IGameTiming>().CurTime;
            return (float) ((Active ? StormEnd : NextStorm) - now).TotalSeconds;
        }
        set
        {
            var target = IoCManager.Resolve<IGameTiming>().CurTime + TimeSpan.FromSeconds(value);

            if (Active)
                StormEnd = target;
            else
                NextStorm = target;
        }
    }
}