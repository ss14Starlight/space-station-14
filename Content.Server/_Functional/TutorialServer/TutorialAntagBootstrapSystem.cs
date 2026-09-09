using Content.Server.Dragon;
using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC.HTN;
using Content.Server.PDA.Ringer;
using Content.Server.Revolutionary;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Server._Starlight.Antags.Vampires;
using Content.Server._Starlight.Antags.Vampires.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Actions;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Ninja.Components;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Strip.Components;
using Content.Shared._Starlight.Roles.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Applies real antag components/mind roles for deep tutorial packages that need them.
/// </summary>
public sealed partial class TutorialAntagBootstrapSystem : EntitySystem
{
    // [Dependency] private ChangelingDevourSystem _changelingDevour = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    // [Dependency] private RevolutionarySystem _revolutionary = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    // [Dependency] private RingerSystem _ringer = default!;
    // [Dependency] private StoreSystem _store = default!;
    // [Dependency] private UplinkSystem _uplink = default!;
    [Dependency] private VampireSystem _vampire = default!;

    private static readonly ProtoId<NpcFactionPrototype> RevolutionaryFaction = "Revolutionary";
    private static readonly EntProtoId MindRoleInitialInfected = "MindRoleInitialInfected";
    private static readonly EntProtoId MindRoleHeadRevolutionary = "MindRoleHeadRevolutionary";
    private static readonly EntProtoId MindRoleChangeling = "MindRoleChangeling";
    private static readonly EntProtoId MindRoleNinja = "MindRoleNinja";
    private static readonly EntProtoId MindRoleXenoborg = "MindRoleXenoborg";
    private static readonly EntProtoId MindRoleMothershipCore = "MindRoleMothershipCore";
    private static readonly EntProtoId MindRoleVampire = "MindRoleVampire";
    private static readonly EntProtoId MindRoleTraitor = "MindRoleTraitor";
    private static readonly EntProtoId MindRoleThief = "MindRoleThief";
    private static readonly EntProtoId MindRoleWizard = "MindRoleWizard";
    private static readonly EntProtoId MindRoleDragon = "MindRoleDragon";
    private static readonly FixedPoint2 TutorialUplinkBalance = 20;
    private static readonly EntProtoId ActionChangelingStore = "ActionChangelingStore";
    private static readonly EntProtoId ActionChangelingStingDna = "ActionChangelingStingDna";
    private static readonly ProtoId<CurrencyPrototype> ChangelingDna = "ChangelingDNA";
    private static readonly ProtoId<StoreCategoryPrototype> ChangelingCombat = "ChangelingStoreCombat";
    private static readonly ProtoId<StoreCategoryPrototype> ChangelingStealth = "ChangelingStoreStealth";
    private static readonly ProtoId<StoreCategoryPrototype> ChangelingUtility = "ChangelingStoreUtility";
    private static readonly EntProtoId PickpocketJumpsuit = "ClothingUniformJumpsuitColorGrey";
    private static readonly EntProtoId PickpocketLoot = "Pen";

    private static readonly TimeSpan TutorialInfectedGrace = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ThiefStripReduction = TimeSpan.FromSeconds(2);

    public void ApplyTutorialAntag(EntityUid mob, EntityUid mindId, ProtoId<AntagPrototype>? antag)
    {
        if (antag == null)
            return;

        switch (antag.Value.Id)
        {
            case "InitialInfected":
                ApplyInitialInfected(mob, mindId);
                break;
            case "HeadRev":
                ApplyHeadRevolutionary(mob, mindId);
                break;
            // case "Changeling":
            //     ApplyChangeling(mob, mindId);
            //     break;
            case "SpaceNinja":
                ApplySpaceNinja(mob, mindId);
                break;
            case "Xenoborg":
                ApplyXenoborg(mindId);
                break;
            case "MothershipCore":
                ApplyMothershipCore(mindId);
                break;
            case "Vampire":
                ApplyVampire(mob, mindId);
                break;
            // case "Traitor":
            //     ApplyTraitor(mob, mindId);
            //     break;
            case "Thief":
                ApplyThief(mob, mindId);
                break;
            case "Wizard":
                ApplyWizard(mindId);
                break;
            case "Dragon":
                ApplyDragon(mob, mindId);
                break;
        }
    }

    /// <summary>
    /// Equips jumpsuit + pocket loot on pickpocket practice victims after they spawn.
    /// </summary>
    public void PrepareThiefPracticeMobs(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<TutorialPracticeMobPickpocketComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            _inventory.SpawnItemInSlot(uid, "jumpsuit", PickpocketJumpsuit.Id, silent: true, force: true);
            _inventory.SpawnItemInSlot(uid, "pocket1", PickpocketLoot.Id, silent: true, force: true);
        }
    }

    private void ApplyThief(EntityUid mob, EntityUid mindId)
    {
        EnsureComp<PacifiedComponent>(mob);
        var thieving = EnsureComp<ThievingComponent>(mob);
        thieving.StripTimeReduction = ThiefStripReduction;
        thieving.Stealthy = true;
        Dirty(mob, thieving);

        if (!_roles.MindHasRole<ThiefRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleThief, silent: true);
    }

    private void ApplyWizard(EntityUid mindId)
    {
        if (!_roles.MindHasRole<WizardRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleWizard, silent: true);
    }

    private void ApplyDragon(EntityUid mob, EntityUid mindId)
    {
        // Spawned MobDragon is a ghost-role NPC — strip takeover + HTN for the tutorial body.
        RemComp<GhostRoleComponent>(mob);
        RemComp<GhostTakeoverAvailableComponent>(mob);
        RemComp<HTNComponent>(mob);

        // Disable the 5-minute rift timeout so practice tips cannot gib the player.
        if (TryComp<DragonComponent>(mob, out var dragon))
        {
            dragon.RiftMaxAccumulator = float.MaxValue;
            dragon.RiftAccumulator = 0f;
        }

        if (!_roles.MindHasRole<DragonRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleDragon, silent: true);
    }

    private void ApplyInitialInfected(EntityUid mob, EntityUid mindId)
    {
        var incurable = EnsureComp<IncurableZombieComponent>(mob);
        EnsureComp<ZombifyOnDeathComponent>(mob);
        EnsureComp<InitialInfectedComponent>(mob);

        var pending = EnsureComp<PendingZombieComponent>(mob);
        pending.GracePeriod = TutorialInfectedGrace;
        pending.MinInitialInfectedGrace = TutorialInfectedGrace;
        pending.MaxInitialInfectedGrace = TutorialInfectedGrace;

        if (incurable.Action == null)
            _actions.AddAction(mob, ref incurable.Action, incurable.ZombifySelfActionPrototype);

        if (!_roles.MindHasRole<InitialInfectedRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleInitialInfected, silent: true);
    }

    private void ApplyHeadRevolutionary(EntityUid mob, EntityUid mindId)
    {
        EnsureComp<RevolutionaryComponent>(mob); // Starlight
        EnsureComp<HeadRevolutionaryComponent>(mob); // Starlight
        _npcFaction.AddFaction(mob, RevolutionaryFaction);

        if (!_roles.MindHasRole<RevolutionaryRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleHeadRevolutionary, silent: true);
    }

    // private void ApplyChangeling(EntityUid mob, EntityUid mindId)
    // {
    //     _changelingDevour.EnsureTutorialDevour(mob);
    //     EnsureComp<ChangelingIdentityComponent>(mob);
    //     EnsureComp<ChangelingTransformComponent>(mob);
    //
    //     var store = EnsureComp<StoreComponent>(mob);
    //     store.Name = "store-preset-name-changeling";
    //     store.Categories = new HashSet<ProtoId<StoreCategoryPrototype>>
    //     {
    //         ChangelingCombat,
    //         ChangelingStealth,
    //         ChangelingUtility,
    //     };
    //     store.CurrencyWhitelist = new HashSet<ProtoId<CurrencyPrototype>> { ChangelingDna };
    //     store.Balance = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>
    //     {
    //         [ChangelingDna] = 60,
    //     };
    //     store.AccountOwner = mindId;
    //
    //     _actions.AddAction(mob, ActionChangelingStore);
    //     _actions.AddAction(mob, ActionChangelingStingDna);
    //
    //     if (!_roles.MindHasRole<ChangelingRoleComponent>(mindId))
    //         _roles.MindAddRole(mindId, MindRoleChangeling, silent: true);
    // }

    private void ApplySpaceNinja(EntityUid mob, EntityUid mindId)
    {
        EnsureComp<SpaceNinjaComponent>(mob);
        if (!_roles.MindHasRole<NinjaRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleNinja, silent: true);
    }

    private void ApplyXenoborg(EntityUid mindId)
    {
        if (!_roles.MindHasRole<XenoborgRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleXenoborg, silent: true);
    }

    private void ApplyMothershipCore(EntityUid mindId)
    {
        if (!_roles.MindHasRole<XenoborgRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleMothershipCore, silent: true);
    }

    private void ApplyVampire(EntityUid mob, EntityUid mindId)
    {
        _vampire.MakeTutorialVampire(mob, classSelectThreshold: 40);
        if (!_roles.MindHasRole<VampireRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MindRoleVampire, silent: true);
    }

    // private void ApplyTraitor(EntityUid mob, EntityUid mindId)
    // {
    //     if (!_roles.MindHasRole<TraitorRoleComponent>(mindId))
    //         _roles.MindAddRole(mindId, MindRoleTraitor, silent: true);
    //
    //     var result = _uplink.AddUplink(mob, TutorialUplinkBalance, out var code, giveDiscounts: false);
    //     if (result == AddUplinkResult.Failure)
    //         return;
    //
    //     // Link + unlock PDA uplink via the generated ringtone code (no player code entry).
    //     var pda = _uplink.FindUplinkTarget(mob);
    //     if (pda == null || code == null)
    //         return;
    //
    //     var storeQuery = EntityQueryEnumerator<StoreComponent>();
    //     while (storeQuery.MoveNext(out var storeUid, out var store))
    //     {
    //         if (store.AccountOwner != mindId)
    //             continue;
    //
    //         _store.SetRemoteStore(pda.Value, storeUid);
    //         _ringer.TryToggleUplink(pda.Value, code, mob);
    //         break;
    //     }
    // }
}
