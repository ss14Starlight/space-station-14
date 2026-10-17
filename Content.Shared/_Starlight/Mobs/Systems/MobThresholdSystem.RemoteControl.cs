using Content.Shared.Alert;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Mobs.Systems;

public sealed partial class MobThresholdSystem
{
    public bool TryGetStateAlert(EntityUid target, MobState state, out ProtoId<AlertPrototype> alert,
        MobThresholdsComponent? thresholdComponent = null)
    {
        if (Resolve(target, ref thresholdComponent, false)
            && thresholdComponent.StateAlertDict.TryGetValue(state, out alert))
            return true;

        alert = default;
        return false;
    }
}
