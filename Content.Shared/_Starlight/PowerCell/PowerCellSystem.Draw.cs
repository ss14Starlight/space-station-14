using Content.Shared.PowerCell.Components;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Content.Shared.PowerCell;

public sealed partial class PowerCellSystem
{
    /// <summary>Sets the draw rate and refreshes any installed power cell.</summary>
    [PublicAPI]
    public void SetDrawRate(Entity<PowerCellDrawComponent?> ent, float newRate)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.DrawRate == newRate)
            return;

        ent.Comp.DrawRate = newRate;
        Dirty(ent, ent.Comp);

        if (TryGetBatteryFromSlot(ent.Owner, out var battery))
            _battery.RefreshChargeRate(battery.Value.AsNullable());
    }
}
