using System.Linq;
using Content.Server._Daiquiri.Xeno.Components;
using Content.Server.Access.Systems;
using Content.Server.Antag;
using Content.Server.Preferences.Managers;
using Content.Shared._Daiquiri.Xeno;
using Content.Shared._Starlight.Overlay.Components;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.PDA;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Species.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Daiquiri.Xeno;

public sealed partial class AlienInfectedSpawnerSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBodySystem _body = default!;

    private static readonly ProtoId<LoadoutGroupPrototype> AssistantJumpsuitGroup = "AssistantJumpsuit";
    private static readonly EntProtoId ThermalVisionAction = "ActionToggleThermal";
    private static readonly EntProtoId InfectedLarvaPrototype = "MobAlienSliderRoyal";
    private static readonly EntProtoId InfectedGibAction = "SliderBreakAction";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlienInfectedSpawnerComponent, AntagSelectEntityEvent>(OnSelectEntity);
        SubscribeLocalEvent<AlienInfectedSpawnerComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagSelected);
    }

    private void OnSelectEntity(Entity<AlienInfectedSpawnerComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled)
            return;

        if (args.Session?.AttachedEntity is { } attached &&
            HasComp<HumanoidAppearanceComponent>(attached))
        {
            args.Entity = attached;
            args.SelectedProfile = GetProfile(args.Antag, args.Session, attached);
            return;
        }

        var infected = Spawn(ent.Comp.Prototype, args.Coords);
        EnsureComp<InfectedSpawnedBodyComponent>(infected);

        args.Entity = infected;
        args.SelectedProfile = GetProfile(args.Antag, args.Session, infected);
    }

    private void OnAfterAntagSelected(Entity<AlienInfectedSpawnerComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var infected = args.EntityUid;
        MakeInfected(infected);

        if (TryComp<InfectedSpawnedBodyComponent>(infected, out _))
        {
            RemComp<InfectedSpawnedBodyComponent>(infected);
            GiveRandomJumpsuit(infected);
            SetIdName(infected);
        }
    }

    private void MakeInfected(EntityUid uid)
    {
        var passive = EnsureComp<PassiveDamageComponent>(uid);
        passive.AllowedStates = new List<MobState> { MobState.Alive, MobState.Critical };
        passive.Damage = new DamageSpecifier();
        passive.Damage.DamageDict["Cellular"] = 0.2f;
        passive.Damage.DamageDict["Bloodloss"] = 0.5f;

        var gib = EnsureComp<GibActionComponent>(uid);
        gib.ActionPrototype = InfectedGibAction;
        gib.PopupText = "С громким треском ваша грудь ломается.";
        gib.AllowedStates = new List<MobState> { MobState.Alive, MobState.Critical, MobState.Dead };

        if (gib.ActionEntity == null)
            _actions.AddAction(uid, ref gib.ActionEntity, gib.ActionPrototype);

        var thermal = EnsureComp<ThermalVisionComponent>(uid);
        if (thermal.ActionEntity == null)
            _actions.AddAction(uid, ref thermal.ActionEntity, ThermalVisionAction);

        if (!TryComp<BodyComponent>(uid, out var body) ||
            !_body.TryGetOrgansWithComponent<BrainComponent>((uid, body), out var brains) ||
            brains.Count == 0)
        {
            return;
        }

        var larva = EnsureComp<AlienLarvaComponent>(brains[0].Owner);
        larva.LarvaPrototype = InfectedLarvaPrototype;
        larva.TransferMind = true;
    }

    private void GiveRandomJumpsuit(EntityUid uid)
    {
        if (!_prototypeManager.TryIndex(AssistantJumpsuitGroup, out var group) ||
            group.Loadouts.Count == 0)
        {
            return;
        }

        var loadoutId = _random.Pick(group.Loadouts);
        if (!_prototypeManager.TryIndex(loadoutId, out LoadoutPrototype? loadout) ||
            !loadout.Equipment.TryGetValue("jumpsuit", out var jumpsuit))
        {
            return;
        }

        if (_inventory.TryGetSlotEntity(uid, "jumpsuit", out _) &&
            _inventory.TryUnequip(uid, "jumpsuit", out var oldJumpsuit, silent: true, force: true) &&
            oldJumpsuit != null)
        {
            QueueDel(oldJumpsuit.Value);
        }

        var item = Spawn(jumpsuit, Transform(uid).Coordinates);
        _inventory.TryEquip(uid, item, "jumpsuit", silent: true, force: true);
    }

    private void SetIdName(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, "id", out var idUid))
            return;

        var cardId = idUid.Value;
        if (TryComp<PdaComponent>(idUid, out var pda) && pda.ContainedId != null)
            cardId = pda.ContainedId.Value;

        if (!TryComp<IdCardComponent>(cardId, out var card))
            return;

        _idCard.TryChangeFullName(cardId, Name(uid), card);
    }

    private HumanoidCharacterProfile GetProfile(AntagSpecifierPrototype antag, ICommonSession? session, EntityUid infected)
    {
        if (session is { } player && _preferences.TryGetCachedPreferences(player.UserId, out var prefs))
        {
            var pool = prefs.Characters.Values
                .OfType<HumanoidCharacterProfile>()
                .Where(profile => profile.Enabled && _antagSelection.IsProfileValidForAntag(player, profile, antag))
                .ToList();

            if (pool.Count > 0)
                return _random.Pick(pool);
        }

        var species = SharedHumanoidAppearanceSystem.DefaultSpecies;
        if (TryComp<HumanoidAppearanceComponent>(infected, out var humanoid))
            species = humanoid.Species;

        return HumanoidCharacterProfile.RandomWithSpecies(species)
            .WithAntagPreferences(antag.PrefRoles);
    }
}
