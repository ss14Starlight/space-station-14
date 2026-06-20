using Content.Shared._Starlight.CosmicCult;
using Content.Shared._Starlight.CosmicCult.Components;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Content.Shared.SSDIndicator;
using Content.Server._Starlight.CosmicCult.Components;
using Content.Shared._Starlight.NullSpace.Components;

namespace Content.Server._Starlight.CosmicCult.Abilities;

public sealed partial class CosmicSiphonSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private CosmicCultRuleSystem _cultRule = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CosmicCultComponent, EventCosmicSiphon>(OnCosmicSiphon);
        SubscribeLocalEvent<CosmicCultComponent, EventCosmicSiphonDoAfter>(OnCosmicSiphonDoAfter);
    }

    private void OnCosmicSiphon(Entity<CosmicCultComponent> uid, ref EventCosmicSiphon args)
    {
        if (args.Handled)
            return;

        foreach (var entity in _lookup.GetEntitiesIntersecting(Transform(uid).Coordinates))
            if (HasComp<NullSpaceBlockerComponent>(entity))
            {
                _popup.PopupEntity(Loc.GetString("cosmicability-generic-fail"), uid, uid);
                return;
            }
        if (uid.Comp.EntropyStored >= uid.Comp.EntropyStoredCap)
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-full"), uid, uid);
            return;
        }
        if (TryComp<SSDIndicatorComponent>(args.Target, out var ssdComp) && ssdComp.IsSSD)
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-fail-ssd", ("target", Identity.Entity(args.Target, EntityManager))), uid, uid);
            return;
        }
        if (HasComp<ActiveNPCComponent>(args.Target) || HasComp<CosmicCultComponent>(args.Target) || !_mobState.IsAlive(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("cosmicability-siphon-fail", ("target", Identity.Entity(args.Target, EntityManager))), uid, uid);
            return;
        }

        var doargs = new DoAfterArgs(EntityManager, uid, uid.Comp.CosmicSiphonDelay, new EventCosmicSiphonDoAfter(), uid, args.Target)
        {
            DistanceThreshold = 2f,
            Hidden = true,
            BreakOnHandChange = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnDropItem = true,
        };
        args.Handled = true;
        _doAfter.TryStartDoAfter(doargs);
    }

    private void OnCosmicSiphonDoAfter(Entity<CosmicCultComponent> uid, ref EventCosmicSiphonDoAfter args)
    {
        if (args.Args.Target is not { } target)
            return;
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        if (TryComp<ActorComponent>(uid, out var actor))
            RaiseNetworkEvent(new CosmicSiphonIndicatorEvent(GetNetEntity(target)), actor.PlayerSession);

        uid.Comp.EntropyStored += uid.Comp.CosmicSiphonQuantity;
        uid.Comp.EntropyBudget += uid.Comp.CosmicSiphonQuantity;
        Dirty(uid, uid.Comp);

        _popup.PopupEntity(Loc.GetString("cosmicability-siphon-success", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        _alerts.ShowAlert(uid.Owner, uid.Comp.EntropyAlert);
        _cultRule.IncrementCultObjectiveEntropy(uid);
        EnsureComp<CosmicDebuffQueueComponent>(target, out var cosmicDebuffQueue);
        cosmicDebuffQueue.DebuffQuant++;
    }
}
