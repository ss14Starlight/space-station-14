using Content.Server._Starlight.Xenoarchaeology.Artifact.XAE.Components;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.CombatMode;
using Content.Shared.NPC.Systems;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// Temporarily turns the artifact into a turret.
/// </summary>
public sealed partial class XAETurretSystem : BaseXAESystem<XAETurretComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XAETurretComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<XAETurretComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SelectedProjectile != null || ent.Comp.PossibleProjectiles.Count == 0)
            return;

        ent.Comp.SelectedProjectile = _random.Pick(ent.Comp.PossibleProjectiles);
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XAETurretActiveComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            // Revert early if it got picked up or put into a container
            if (_timing.CurTime < active.ActiveUntil && !_container.IsEntityInContainer(uid))
                continue;

            Revert((uid, active));
        }
    }

    /// <inheritdoc />
    protected override void OnActivated(Entity<XAETurretComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var comp = ent.Comp;
        if (comp.SelectedProjectile is not { } projectile)
            return;

        var artifact = args.Artifact.Owner;

        // Stops held artifacts from turning into turrets when in a container
        if (_container.IsEntityInContainer(artifact))
            return;

        var activeUntil = _timing.CurTime + comp.Duration;

        // If already a turret from another node extend the duration.
        if (TryComp<XAETurretActiveComponent>(artifact, out var active))
        {
            if (activeUntil > active.ActiveUntil)
                active.ActiveUntil = activeUntil;
            return;
        }

        // Dont turn it into a gun if it already is one
        if (HasComp<GunComponent>(artifact))
            return;

        // GunComponent is access-restricted. FireRate is calculated using the ammo recharge cooldown instead
        var gun = AddComp<GunComponent>(artifact);
        _gun.RefreshModifiers((artifact, gun));
        _gun.SetAvailableModes((artifact, gun), SelectiveFire.FullAuto);

        var ammoProvider = AddComp<BasicEntityAmmoProviderComponent>(artifact);
        ammoProvider.Proto = projectile;
        // Null capacity is infinite ammo so it would fire at max firerate
        ammoProvider.Capacity = 1;
        ammoProvider.Count = 1;
        Dirty(artifact, ammoProvider);

        var recharge = AddComp<RechargeBasicEntityAmmoComponent>(artifact);
        recharge.RechargeCooldown = 1f / comp.FireRate;
        Dirty(artifact, recharge);

        _faction.AddFaction(artifact, comp.Faction);

        // Adds combat mode to the artifact so it can shoot
        EnsureComp<CombatModeComponent>(artifact);

        var htn = AddComp<HTNComponent>(artifact);
        htn.RootTask = new HTNCompoundTask { Task = "TurretCompound" };
        htn.Blackboard.SetValue("RangedRange", comp.Range);

        active = AddComp<XAETurretActiveComponent>(artifact);
        active.ActiveUntil = activeUntil;
        active.Faction = comp.Faction;
    }

    private void Revert(Entity<XAETurretActiveComponent> ent)
    {
        var artifact = ent.Owner;

        // Removing the HTN doesn't shut down the running gun task so it needs to be removed here
        RemComp<NPCRangedCombatComponent>(artifact);
        RemComp<HTNComponent>(artifact);
        RemComp<CombatModeComponent>(artifact);
        RemComp<RechargeBasicEntityAmmoComponent>(artifact);
        RemComp<BasicEntityAmmoProviderComponent>(artifact);
        RemComp<GunComponent>(artifact);
        _faction.RemoveFaction(artifact, ent.Comp.Faction);
        RemCompDeferred<XAETurretActiveComponent>(artifact);
    }
}
