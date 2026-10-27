using Content.Shared.NodeContainer;
using Robust.Shared.Timing;

// ReSharper disable CheckNamespace
namespace Content.Server.Electrocution;

public sealed partial class ElectrocutionSystem
{
    [Dependency] private IGameTiming _timing = default!;

    // Give a victim time to recover after an electrified source's shock ends.
    // The cooldown lives on the victim so changing sources cannot reset it.
    private static readonly TimeSpan ShockRecoveryTime = TimeSpan.FromSeconds(1);

    private bool CanReceiveElectrifiedShock(EntityUid target)
    {
        return !TryComp<ElectrifiedShockCooldownComponent>(target, out var cooldown) ||
               _timing.CurTime >= cooldown.NextShockAt;
    }

    private bool TryDoUnpoweredElectrifiedShock(EntityUid target, EntityUid source, int damage,
        TimeSpan shockTime, float siemensCoefficient, bool useCooldown, bool bypassCooldown)
    {
        if (useCooldown && !bypassCooldown && !CanReceiveElectrifiedShock(target))
            return false;

        var shocked = TryDoElectrocution(target, source, damage, shockTime, true, siemensCoefficient);
        if (useCooldown && shocked)
            SetElectrifiedShockCooldown(target, shockTime);

        return shocked;
    }

    private bool TryDoPoweredElectrifiedShock(EntityUid target, EntityUid source, Node node, int damage,
        TimeSpan shockTime, float siemensCoefficient, bool useCooldown, bool bypassCooldown)
    {
        if (useCooldown && !bypassCooldown && !CanReceiveElectrifiedShock(target))
            return false;

        var shocked = TryDoElectrocutionPowered(target, source, node, damage, shockTime, true, siemensCoefficient);
        if (useCooldown && shocked)
            SetElectrifiedShockCooldown(target, shockTime);

        return shocked;
    }

    private void SetElectrifiedShockCooldown(EntityUid target, TimeSpan shockTime)
    {
        var cooldown = EnsureComp<ElectrifiedShockCooldownComponent>(target);
        cooldown.NextShockAt = _timing.CurTime + shockTime + ShockRecoveryTime;
    }
}
