using Content.Shared._Afterlight.Silicons.Borgs;
using Content.Shared._Starlight.Silicons.Laws;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Silicons.Borgs;

/// <summary>
/// Sent when a player uses the borg BUI to reset a borg's chassis type.
/// </summary>
[Serializable, NetSerializable]
public sealed class BorgResetChassisBuiMessage : BoundUserInterfaceMessage;

/// <summary>
/// Raised on a borg once the chassis reset has been worked on for long enough.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class BorgResetChassisDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Lets a borg's chassis be returned to the state it was in before a type was picked, so it can pick again.
/// </summary>
public sealed partial class BorgChassisResetSystem : EntitySystem
{
    /// <summary>
    /// A borg type describing the blank chassis. Applying it strips whatever the previous type added.
    /// </summary>
    public static readonly ProtoId<BorgTypePrototype> UnselectedType = "unselected";

    private static readonly TimeSpan ResetDelay = TimeSpan.FromSeconds(3);

    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedBorgSystem _borg = default!;
    [Dependency] private SharedBorgSwitchableTypeSystem _switchableType = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgSwitchableTypeComponent, BorgResetChassisBuiMessage>(OnResetChassis);
        SubscribeLocalEvent<BorgSwitchableTypeComponent, BorgResetChassisDoAfterEvent>(OnResetChassisDoAfter);
    }

    /// <summary>
    /// Whether this borg picked a type that it could pick again after a reset.
    /// </summary>
    public bool CanReset(Entity<BorgSwitchableTypeComponent> borg) =>
        borg.Comp.SelectedBorgType is { } type && type != UnselectedType && borg.Comp.AvailableTypes.Count > 0
        && HasComp<BorgObeysStationAiComponent>(borg);

    /// <summary>
    /// Whether every module a player is allowed to take out has been taken out.
    /// </summary>
    public bool OptionalModulesRemoved(Entity<BorgChassisComponent?> borg)
    {
        if (!Resolve(borg, ref borg.Comp, false))
            return true;

        foreach (var module in borg.Comp.ModuleContainer.ContainedEntities)
        {
            if (TryComp<BorgModuleComponent>(module, out var moduleComp) && _borg.CanRemoveModule((module, moduleComp)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a borg to its unselected chassis and gives back the type selection action.
    /// </summary>
    public void ResetChassis(Entity<BorgSwitchableTypeComponent> borg)
    {
        // Drop the cosmetic subtype first, the appearance code skips borgs that still have one.
        if (TryComp<BorgSwitchableSubtypeComponent>(borg, out var subtype))
        {
            if (_prototypes.TryIndex(subtype.BorgSubtype, out var subtypeProto)
                && subtypeProto.TryComp<BorgSubtypeDefinitionComponent>(out var definition, _componentFactory)
                && definition.AddComponents is { } subtypeComponents)
            {
                EntityManager.RemoveComponents(borg, subtypeComponents);
            }

            subtype.BorgSubtype = null;
            Dirty(borg.Owner, subtype);
        }

        // Take the modules out before deleting them so hands and held items get cleaned up properly.
        if (TryComp<BorgChassisComponent>(borg, out var chassis))
        {
            foreach (var module in new List<EntityUid>(chassis.ModuleContainer.ContainedEntities))
            {
                _container.Remove(module, chassis.ModuleContainer);
                QueueDel(module);
            }
        }

        // Applying the blank type reuses the normal switching logic, which removes the old type's
        // components and resets modules, radio channels, inventory, transponder and appearance.
        // The blank type stays selected so that appearance code keeps having a prototype to work from,
        // selecting a real type again is allowed out of it.
        _switchableType.SelectBorgModule(borg, UnselectedType);

        EnsureComp<BorgChassisResetComponent>(borg);

        _actions.AddAction(borg, ref borg.Comp.SelectTypeAction, SharedBorgSwitchableTypeSystem.ActionId);
        Dirty(borg);
    }

    private void OnResetChassis(Entity<BorgSwitchableTypeComponent> borg, ref BorgResetChassisBuiMessage args)
    {
        if (!CanReset(borg))
            return;

        if (!OptionalModulesRemoved(borg.Owner))
        {
            _popup.PopupEntity(Loc.GetString("borg-reset-modules-installed"), borg, args.Actor);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager,
            args.Actor,
            ResetDelay,
            new BorgResetChassisDoAfterEvent(),
            borg,
            target: borg)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("borg-reset-chassis-start-popup"), borg, borg, PopupType.LargeCaution);
    }

    private void OnResetChassisDoAfter(Entity<BorgSwitchableTypeComponent> borg, ref BorgResetChassisDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !CanReset(borg))
            return;

        if (_net.IsClient) // Starlight
            return; // Starlight

        if (!OptionalModulesRemoved(borg.Owner))
        {
            _popup.PopupEntity(Loc.GetString("borg-reset-modules-installed"), borg, args.User);
            return;
        }

        args.Handled = true;

        ResetChassis(borg);
        _popup.PopupEntity(Loc.GetString("borg-reset-chassis-popup", ("name", Name(borg))), borg);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):player} reset the chassis type of borg {ToPrettyString(borg.Owner)}");
    }
}
