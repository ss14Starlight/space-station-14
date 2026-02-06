using Content.Server.Objectives.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Components;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mind;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Starlight.Antags.Abductor;
using Content.Shared.Starlight.ItemSwitch;
using Content.Shared.UserInterface;
using Robust.Shared.Spawners;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Server.VendingMachines;
using Content.Shared.VendingMachines;
using Robust.Shared.Audio;

namespace Content.Server.Starlight.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly NumberObjectiveSystem _number = default!;
    [Dependency] private readonly SharedItemSwitchSystem _itemSwitch = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly VendingMachineSystem _vending = default!;

    private readonly SoundSpecifier _sendSound = new SoundPathSpecifier("/Audio/Voice/Human/wilhelm_scream.ogg");
    private readonly SoundSpecifier _alienTeleport = new SoundPathSpecifier("/Audio/_Starlight/Misc/alien_teleport.ogg");

    public void InitializeConsole()
    {
        SubscribeLocalEvent<AbductorConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<AbductConditionComponent, ObjectiveGetProgressEvent>(OnAbductGetProgress);

        Subs.BuiEvents<AbductorConsoleComponent>(AbductorConsoleUIKey.Key, subs =>
        {
            subs.Event<AbductorAttractBuiMsg>(OnAttractBuiMsg);
            subs.Event<AbductorCompleteExperimentBuiMsg>(OnCompleteExperimentBuiMsg);
            subs.Event<AbductorVestModeChangeBuiMsg>(OnVestModeChangeBuiMsg);
            subs.Event<AbductorLockBuiMsg>(OnVestLockBuiMsg);
            subs.Event<AbductorItemBuyedBuiMsg>(OnItemBuyedBuiMsg);
        });

        SubscribeLocalEvent<AbductorComponent, AbductorAttractDoAfterEvent>(OnDoAfterAttract);
    }
    private void OnAbductGetProgress(Entity<AbductConditionComponent> ent, ref ObjectiveGetProgressEvent args) 
        => args.Progress = AbductProgress(ent, _number.GetTarget(ent.Owner));

    private float AbductProgress(Entity<AbductConditionComponent> ent, int target)
    {
        AbductConditionComponent? linkedAbduct = null;

        if (TryComp<AbductorScientistComponent>(ent, out var scientistComp))
            linkedAbduct = TryGetAbductedCondition(scientistComp.Agent);

        if (TryComp<AbductorAgentComponent>(ent, out var agentComp))
            linkedAbduct = TryGetAbductedCondition(agentComp.Scientist);

        if (linkedAbduct != null && linkedAbduct.Abducted > ent.Comp.Abducted)
            ent.Comp.Abducted = linkedAbduct.Abducted;

        return target == 0 ? 1f : MathF.Min(ent.Comp.Abducted / (float)target, 1f);
    }

    private AbductConditionComponent? TryGetAbductedCondition(EntityUid? other)
    {
        if (!TryComp<MindContainerComponent>(other, out var mindContainer) || !mindContainer.Mind.HasValue)
            return null;

        var mindId = mindContainer.Mind.Value;

        if (!TryComp<MindComponent>(mindId, out var mind))
            return null;

        var objEntity = mind.Objectives.FirstOrDefault(HasComp<AbductConditionComponent>);
        return TryComp<AbductConditionComponent>(objEntity, out var abduct) ? abduct : null;
    }

    private void OnVestModeChangeBuiMsg(EntityUid uid, AbductorConsoleComponent component, AbductorVestModeChangeBuiMsg args)
    {
        if (component.Armor != null)
            _itemSwitch.Switch(GetEntity(component.Armor.Value), args.Mode.ToString());
    }

    private void OnItemBuyedBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorItemBuyedBuiMsg args)
    {
        var xform = EnsureComp<TransformComponent>(ent);

        if (ent.Comp.Balance >= args.Price)
        {
            ent.Comp.Balance -= args.Price;
            Spawn(args.Item, xform.Coordinates);
        }
    }

    private void OnVestLockBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorLockBuiMsg args)
    {
        if (ent.Comp.Armor is not null && GetEntity(ent.Comp.Armor.Value) is EntityUid armor)
        {
            if (TryComp<UnremoveableComponent>(armor, out var unremoveable))
                RemComp(armor, unremoveable);
            else
                AddComp<UnremoveableComponent>(armor);
        }
    }

    private void OnCompleteExperimentBuiMsg(EntityUid uid, AbductorConsoleComponent component, AbductorCompleteExperimentBuiMsg args)
    {
        var experimentatorId = GetEntity(component.Experimentator);

        if (!TryComp<AbductorExperimentatorComponent>(experimentatorId, out var experimentatorComp))
            return;

        var container = _container.GetContainer(experimentatorId.Value, experimentatorComp.ContainerId);
        var victim = container.ContainedEntities.FirstOrDefault(HasComp<AbductorVictimComponent>);

        if (victim == default || !TryComp(victim, out AbductorVictimComponent? victimComp))
            return;

        TryUpdateObjectiveProgress(args.Actor, victim, victimComp, component);

        _audioSystem.PlayPvs(_sendSound, experimentatorId.Value);

        if (victimComp.Position is { } position)
            _xformSys.SetCoordinates(victim, position);
    }

    private void TryUpdateObjectiveProgress(EntityUid actor, EntityUid victim, AbductorVictimComponent victimComp, AbductorConsoleComponent component)
    {
        if (victimComp.Organ == AbductorOrganType.None 
            || !TryComp<MindContainerComponent>(actor, out var mindContainer) 
            || mindContainer.Mind is not { } mindId
            || !TryComp<MindComponent>(mindId, out var mind))
            return;

        var objId = mind.Objectives.FirstOrDefault(HasComp<AbductConditionComponent>);
        if (objId == default 
            || !TryComp<AbductConditionComponent>(objId, out var condition))
            return;

        var victimNet = GetNetEntity(victim);
        if (condition.AbductedHashs.Contains(victimNet))
            return;

        condition.AbductedHashs.Add(victimNet);
        condition.Abducted++;
        component.Balance++;
    }

    private void OnAttractBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorAttractBuiMsg args)
    {
        if (ent.Comp.Target == null || ent.Comp.AlienPod == null || ent.Comp.Dispencer == null)
            return;

        var target = GetEntity(ent.Comp.Target.Value);
        EnsureComp<TransformComponent>(target, out var xform);

        var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, target);

        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);
        despawnEffectEntComp.Lifetime = 3.0f;

        _audioSystem.PlayPvs(_alienTeleport, effectEnt);

        var telepad = GetEntity(ent.Comp.AlienPod.Value);
        var telepadXform = EnsureComp<TransformComponent>(telepad);
        var effect = _entityManager.SpawnEntity(_teleportationEffectEntity, telepadXform.Coordinates);

        EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);
        despawnComp.Lifetime = 3.0f;

        _audioSystem.PlayPvs(_alienTeleport, effect);

        var dispencer = ent.Comp.Dispencer;

        var @event = new AbductorAttractDoAfterEvent(GetNetCoordinates(telepadXform.Coordinates), GetNetEntity(target), dispencer.Value);
        ent.Comp.Target = null;
        var doAfter = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(3), @event, args.Actor)
        {
            BreakOnDamage = false,
            BreakOnDropItem = false,
            BreakOnHandChange = false,
            BreakOnMove = false,
            BreakOnWeightlessMove = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }
    private void OnDoAfterAttract(Entity<AbductorComponent> ent, ref AbductorAttractDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var victim = GetEntity(args.Victim);

        if (_pullingSystem.IsPulling(victim) 
            || !TryComp<PullerComponent>(victim, out var pullerComp) 
            || !TryComp<PullableComponent>(pullerComp.Pulling, out var pullableComp) 
            || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp))
            return;

        if (_pullingSystem.IsPulled(victim) 
            || !_pullingSystem.TryStopPull(victim, pullableComp))
            return;

        if (!HasComp<AbductorComponent>(victim))
        {
            var dispenser = GetEntity(args.Dispencer);

            if (TryComp<VendingMachineComponent>(dispenser, out var vendingComp))
                _vending.RestockRandom(dispenser, vendingComp);
        }
        else if (_mobState.IsDead(victim)) //The console can grab em if crit. but nothing less.
            return;
        
        _xformSys.SetCoordinates(victim, GetCoordinates(args.TargetCoordinates));
    }

    private void OnBeforeActivatableUIOpen(Entity<AbductorConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args) => UpdateGui(ent.Comp.Target, ent);

    public void SyncAbductors(Entity<AbductorConsoleComponent> ent)
    {
        if (ent.Comp.Agent == null && ent.Comp.Scientist == null)
            return;

        if (TryComp<AbductorScientistComponent>(ent.Comp.Scientist, out var scientistComp))
            scientistComp.Agent = ent.Comp.Agent;

        if (TryComp<AbductorAgentComponent>(ent.Comp.Agent, out var agentComp))
            agentComp.Scientist = ent.Comp.Scientist;
    }

    protected override void UpdateGui(NetEntity? target, Entity<AbductorConsoleComponent> computer)
    {
        string? targetName = null;
        string? victimName = null;
        if (target.HasValue && TryComp(GetEntity(target.Value), out MetaDataComponent? metadata))
            targetName = metadata?.EntityName;

        if (computer.Comp.AlienPod == null)
        {
            var xform = EnsureComp<TransformComponent>(computer.Owner);
            var alienpad = _entityLookup.GetEntitiesInRange<AbductorAlienPadComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (alienpad != default)
                computer.Comp.AlienPod = GetNetEntity(alienpad);
        }

        if (computer.Comp.Experimentator == null)
        {
            var xform = EnsureComp<TransformComponent>(computer.Owner);
            var experimentator = _entityLookup.GetEntitiesInRange<AbductorExperimentatorComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (experimentator != default)
                computer.Comp.Experimentator = GetNetEntity(experimentator);
        }

        if (computer.Comp.Experimentator != null
            && GetEntity(computer.Comp.Experimentator) is EntityUid experimentatorId
            && TryComp<AbductorExperimentatorComponent>(experimentatorId, out var experimentatorComp))
        {
            var container = _container.GetContainer(experimentatorId, experimentatorComp.ContainerId);
            var victim = container.ContainedEntities.FirstOrDefault(e => HasComp<AbductorVictimComponent>(e));
            if (victim != default && TryComp(victim, out MetaDataComponent? victimMetadata))
                victimName = victimMetadata?.EntityName;
        }

        if (computer.Comp.Dispencer == null)
        {
            var xform = EnsureComp<TransformComponent>(computer.Owner);
            var dispencer = _entityLookup.GetEntitiesInRange<AbductorDispencerComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (dispencer != default)
                computer.Comp.Dispencer = GetNetEntity(dispencer);
        }

        var armorLock = false;
        var armorMode = AbductorArmorModeType.Stealth;

        if (computer.Comp.Armor != null)
        {
            if (HasComp<UnremoveableComponent>(GetEntity(computer.Comp.Armor.Value)))
                armorLock = true;
            
            if (TryComp<ItemSwitchComponent>(GetEntity(computer.Comp.Armor.Value), out var switchVest)
                && Enum.TryParse<AbductorArmorModeType>(switchVest.State, ignoreCase: true, out var state))
                armorMode = state;
        }

        _uiSystem.SetUiState(computer.Owner, AbductorConsoleUIKey.Key, new AbductorConsoleBuiState()
        {
            Target = target,
            TargetName = targetName,
            VictimName = victimName,
            AlienPadFound = computer.Comp.AlienPod != default,
            ExperimentatorFound = computer.Comp.Experimentator != default,
            DispencerFound = computer.Comp.Dispencer != default,
            ArmorFound = computer.Comp.Armor != default,
            ArmorLocked = armorLock,
            CurrentArmorMode = armorMode,
            CurrentBalance = computer.Comp.Balance
        });
    }
}
