using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.Components;
using Content.Shared.Devour.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

#region "Starlight"
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
#endregion

namespace Content.Shared.Devour;

public sealed partial class DevourSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private SharedActionsSystem _actionsSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private DamageableSystem _damageSystem = default!; //Starlight
    [Dependency] private MobThresholdSystem _thresholdSystem = default!; //Starlight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DevourerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<DevourerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DevourerComponent, DevourActionEvent>(OnDevourAction);
        SubscribeLocalEvent<DevourerComponent, DevourDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DevourerComponent, GibbedBeforeDeletionEvent>(OnGibContents);
    }

    private void OnStartup(Entity<DevourerComponent> ent, ref ComponentStartup args)
    {
        //Devourer doesn't actually chew, since he sends targets right into his stomach.
        //I did it mom, I added ERP content into upstream. Legally!
        ent.Comp.Stomach = _containerSystem.EnsureContainer<Container>(ent.Owner, DevourerComponent.StomachContainerId);
    }

    private void OnInit(Entity<DevourerComponent> ent, ref MapInitEvent args)
    {
        _actionsSystem.AddAction(ent.Owner, ref ent.Comp.DevourActionEntity, ent.Comp.DevourAction);
    }

    private void OnShutdown(Entity<DevourerComponent> ent, ref ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(ent.Owner, ent.Comp.DevourActionEntity);
    }

    /// <summary>
    /// The devour action
    /// </summary>
    private void OnDevourAction(Entity<DevourerComponent> ent, ref DevourActionEvent args)
    {
        if (args.Handled || !_whitelistSystem.CheckBoth(args.Target, ent.Comp.Blacklist, ent.Comp.Whitelist)) // Starlight-edit - IsWhitelistFailOrNull -> !CheckBoth
            return;

        args.Handled = true;
        var target = args.Target;

        // Structure and mob devours handled differently.
        if (TryComp(target, out MobStateComponent? targetState))
        {
            switch (targetState.CurrentState)
            {
                case MobState.Critical:
                case MobState.Dead:

                    _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.DevourTime, new DevourDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
                    {
                        BreakOnMove = true,
                    });
                    break;
                case MobState.Invalid:
                case MobState.Alive:
                default:
                    _popupSystem.PopupClient(Loc.GetString("devour-action-popup-message-fail-target-alive"), ent.Owner, ent.Owner);
                    break;
            }

            return;
        }

        _popupSystem.PopupClient(Loc.GetString("devour-action-popup-message-structure"), ent.Owner, ent.Owner);

        if (ent.Comp.SoundStructureDevour != null)
            _audioSystem.PlayPredicted(ent.Comp.SoundStructureDevour, ent.Owner, ent.Owner, ent.Comp.SoundStructureDevour.Params);

        _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.StructureDevourTime, new DevourDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<DevourerComponent> ent, ref DevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        //Starlight-Start
        if (args.Target is not { } target)
            return;

        if (!TryComp(target, out DamageableComponent? damageable))
            return;

        //Specific ammount of damage to apply to kill the target
        var deathThreshold = _thresholdSystem.GetThresholdForState(target, MobState.Dead);
        var targetDamage = _damageSystem.GetDamage((target, damageable));
        var targetDamageTotal = targetDamage.GetTotal();
        var requiredDamage = deathThreshold - targetDamageTotal;

        //Only apply if they aren't dead
        if (requiredDamage > 0)
        {
            var damageToApply = new DamageSpecifier { DamageDict = { { "Asphyxiation", requiredDamage } } };

            if (!_damageSystem.TryChangeDamage(target, damageToApply)) //This will run as backup if airloss damage cannot be applied
            {
                damageToApply = new DamageSpecifier { DamageDict = { { "Caustic", requiredDamage } } };
                _damageSystem.TryChangeDamage(target, damageToApply);
            }
        }
        //Starlight-End

        var ichorInjection = new Solution(ent.Comp.Chemical, ent.Comp.HealRate);

        // Grant ichor if the devoured thing meets the dragon's food preference
        if (target != null && _whitelistSystem.IsWhitelistPassOrNull(ent.Comp.FoodPreferenceWhitelist, (EntityUid)target)) //Starlight, args.Args.Target replaced with target
        {
            _bloodstreamSystem.TryAddToBloodstream(ent.Owner, ichorInjection);
            ent.Comp.Devoured++; //Starlight devour counter.
        }

        // If the devoured thing meets the stomach whitelist criteria, add it to the stomach
        if (target != null && _whitelistSystem.IsWhitelistPass(ent.Comp.StomachStorageWhitelist, (EntityUid)target)) //Starlight, args.Args.Target replaced with target
        {
            _containerSystem.Insert(target, ent.Comp.Stomach); //starlight target.value replaced with target
        }
        //TODO: Figure out a better way of removing structures via devour that still entails standing still and waiting for a DoAfter. Somehow.
        //If it's not alive, it must be a structure.
        // Delete if the thing isn't in the stomach storage whitelist (or the stomach whitelist is null/empty)
        else if (target != null) //Starlight, args.Args.Target replaced with target
        {
            PredictedQueueDel(target); //starlight target.value replaced with target
        }

        _audioSystem.PlayPredicted(ent.Comp.SoundDevour, ent.Owner, ent.Owner);
    }

    private void OnGibContents(Entity<DevourerComponent> ent, ref GibbedBeforeDeletionEvent args)
    {
        if (ent.Comp.StomachStorageWhitelist == null)
            return;

        _containerSystem.EmptyContainer(ent.Comp.Stomach);
    }
}

public sealed partial class DevourActionEvent : EntityTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class DevourDoAfterEvent : SimpleDoAfterEvent;

