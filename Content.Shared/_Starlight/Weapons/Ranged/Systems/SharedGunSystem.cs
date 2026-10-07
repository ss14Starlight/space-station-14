using Content.Shared.Weapons.Ranged.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

// Helpers for firing a gun that has no wielder
public abstract partial class SharedGunSystem
{
    /// <summary>
    /// Bolts a gun and chambers a round for an unheld gun so the next shot fires.
    /// </summary>
    public void ForceChamber(Entity<GunComponent?> gun)
    {
        if (!Resolve(gun, ref gun.Comp, false))
            return;

        if (!TryComp<ChamberMagazineAmmoProviderComponent>(gun, out var chamber))
            return;

        // Closing an open bolt also cycles a round into the chamber.
        if (chamber.BoltClosed == false)
            SetBoltClosed(gun, chamber, true);

        // Cycles once if the chamber is empty with a closed bolt
        if (GetChamberEntity(gun) == null)
            CycleCartridge(gun, chamber);
    }

    /// <summary>
    /// Cycles an unheld gun
    /// </summary>
    public void ForceCycle(Entity<GunComponent?> gun)
    {
        if (!Resolve(gun, ref gun.Comp, false))
            return;

        if (gun.Comp.Pump &&
            TryComp<BallisticAmmoProviderComponent>(gun, out var ballistic))
        {
            ManualCycle((gun.Owner, ballistic), TransformSystem.GetMapCoordinates(gun));
            return;
        }

        if (TryComp<ChamberMagazineAmmoProviderComponent>(gun, out var chamber) &&
            !chamber.AutoCycle && chamber.BoltClosed != null)
        {
            // Open ejects the spent round. Close inserts a unused round from the magazine.
            SetBoltClosed(gun, chamber, false);
            SetBoltClosed(gun, chamber, true);
        }
    }
}
