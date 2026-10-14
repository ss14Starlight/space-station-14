using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Lock;
using Robust.Shared.Network;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed partial class BatteryWeaponFireModesSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;

#region Starlight
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private INetManager _net = default!;
#endregion Starlight


    [SubscribeLocalEvent] // Starlight-edit
    private void OnExamined(Entity<BatteryWeaponFireModesComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.FireModes.Count < 2)
            return;

        var fireMode = GetMode(ent.Comp);

        // Starlight-start
        if (TryGetAmmoProvider(ent, out var ammoProvider) && ammoProvider != null)
        {
            if (ammoProvider is BatteryAmmoProviderComponent)
            {
                // Hitscan prototypes aren't indexed as EntityPrototype — skip examine line if not found
                if (_prototypeManager.TryIndex<EntityPrototype>(fireMode.Prototype, out var projectile))
                    args.PushMarkup(Loc.GetString("gun-set-fire-mode-examine", ("mode", projectile.Name)));
            }
        }
        // Starlight-end
    }

    private BatteryWeaponFireMode GetMode(BatteryWeaponFireModesComponent component)
        => component.FireModes[component.CurrentFireMode]; // Starlight-edit

    [SubscribeLocalEvent] // Starlight-edit
    private void OnGetVerb(EntityUid uid, BatteryWeaponFireModesComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (component.FireModes.Count < 2)
            return;

        if (TryComp<LockComponent>(uid, out var lockComponent) && lockComponent.Locked)
            return;

        if (!_accessReaderSystem.IsAllowed(args.User, uid))
            return;

        if (!TryGetAmmoProvider(uid, out var ammoProvider) && ammoProvider == null) // Starlight-edit
            return;

        for (var i = 0; i < component.FireModes.Count; i++)
        {
            var fireMode = component.FireModes[i];
            var index = i;

            // Starlight-start
            if (ammoProvider is BatteryAmmoProviderComponent)
            {
                var label = _prototypeManager.TryIndex<EntityPrototype>(fireMode.Prototype, out var entProto)
                    ? entProto.Name
                    : fireMode.Prototype;

                var v = new Verb
                {
                    Priority = 1,
                    Category = VerbCategory.SelectType,
                    Text = label,
                    Disabled = i == component.CurrentFireMode,
                    Impact = LogImpact.Low,
                    DoContactInteraction = true,
                    Act = () =>
                    {
                        SetFireMode((uid, component), index, args.User);
                    }
                };

                args.Verbs.Add(v);
            }
            // Starlight-end
        }
    }

    [SubscribeLocalEvent] // Starlight-edit
    private void OnUseInHandEvent(Entity<BatteryWeaponFireModesComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryCycleFireMode(ent, args.User);
    }

    public void TryCycleFireMode(Entity<BatteryWeaponFireModesComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.FireModes.Count < 2)
            return;

        var index = (ent.Comp.CurrentFireMode + 1) % ent.Comp.FireModes.Count;
        TrySetFireMode(ent, index, user);
    }

    public bool TrySetFireMode(Entity<BatteryWeaponFireModesComponent> ent, int index, EntityUid? user = null)
    {
        if (index < 0 || index >= ent.Comp.FireModes.Count)
            return false;

        if (user != null && !_accessReaderSystem.IsAllowed(user.Value, ent))
            return false;

        SetFireMode(ent, index, user);

        return true;
    }

    private void SetFireMode(Entity<BatteryWeaponFireModesComponent> ent, int index, EntityUid? user = null)
    {
        // Starlight-start
        var fireMode = ent.Comp.FireModes[index];

        // Conditions only exist on the server, so the client predicts only the modes without them.
        if (_net.IsClient && fireMode.ServerOnly)
            return;

        if (fireMode.Conditions != null && user != null)
        {
            var conditionArgs = new FireModeConditionConditionArgs(user.Value, ent, fireMode, EntityManager);
            var conditionsMet = fireMode.Conditions.All(condition => condition.Condition(conditionArgs));

            if (!conditionsMet)
                return;
        }
        // Starlight-end

        ent.Comp.CurrentFireMode = index;
        Dirty(ent);

        // Starlight-start
        if (TryGetAmmoProvider(ent, out var ammoProvider) && ammoProvider != null)
        {
            if (ammoProvider is BatteryAmmoProviderComponent projectileAmmo)
            {
                var oldFireCost = projectileAmmo.FireCost;
                projectileAmmo.Prototype = fireMode.Prototype;
                projectileAmmo.FireCost = fireMode.FireCost;

                float fireCostDiff = (float)fireMode.FireCost / (float)oldFireCost;
                projectileAmmo.Shots = (int)Math.Round(projectileAmmo.Shots / fireCostDiff);
                projectileAmmo.Capacity = (int)Math.Round(projectileAmmo.Capacity / fireCostDiff);
                Dirty(ent, projectileAmmo);

                if (user != null && _prototypeManager.TryIndex<EntityPrototype>(fireMode.Prototype, out var prototype))
                    _popupSystem.PopupPredicted(Loc.GetString("gun-set-fire-mode-popup", ("mode", prototype.Name)), ent, user);
            }

            if (fireMode.HeldPrefix != null)
                _item.SetHeldPrefix(ent, fireMode.HeldPrefix);
        }
        // Starlight-end

        if (TryComp(ent, out BatteryAmmoProviderComponent? batteryAmmoProviderComponent))
        {
            batteryAmmoProviderComponent.Prototype = fireMode.Prototype;
            batteryAmmoProviderComponent.FireCost = fireMode.FireCost;

            Dirty(ent, batteryAmmoProviderComponent);

            _gun.UpdateShots((ent, batteryAmmoProviderComponent));
        }

        // Starlight-start: per-fire-mode gunshot sound override
        if (TryComp(ent, out GunComponent? gunComp))
            _gun.RefreshModifiers((ent, gunComp));
        // Starlight-end
    }

    #region Starlight

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<BatteryWeaponFireModesComponent> ent, ref MapInitEvent args)
    {
        foreach (var mode in ent.Comp.FireModes)
            mode.ServerOnly = mode.Conditions is { Count: > 0 };

        Dirty(ent);
    }

    private bool TryGetAmmoProvider(EntityUid uid, out object? ammoProvider)
    {
        ammoProvider = null;

        if (TryComp<BatteryAmmoProviderComponent>(uid, out var provider))
        {
            ammoProvider = provider;
            return true;
        }

        return false;
    }

    [SubscribeLocalEvent]
    private void OnShootAttempt(Entity<BatteryWeaponFireModesComponent> ent, ref AttemptShootEvent args)
    {

        var fireMode = ent.Comp.FireModes[ent.Comp.CurrentFireMode];

        if (fireMode.Conditions != null)
        {
            var conditionArgs = new FireModeConditionConditionArgs(args.User, ent, fireMode, EntityManager);
            var conditionsMet = fireMode.Conditions.All(condition => condition.Condition(conditionArgs));

            if (!conditionsMet)
                SetFireMode(ent, 0, args.User);
        }
    }

    [SubscribeLocalEvent]
    private void OnInteractHandEvent(Entity<BatteryWeaponFireModesComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (ent.Comp.FireModes.Count < 2)
            return;

        CycleFireMode(ent, args.User);
    }

    private void CycleFireMode(Entity<BatteryWeaponFireModesComponent> ent, EntityUid user)
    {
        if (ent.Comp.FireModes.Count < 2)
            return;

        var index = (ent.Comp.CurrentFireMode + 1) % ent.Comp.FireModes.Count;

        SetFireMode(ent, index, user);
    }

    [SubscribeLocalEvent]
    private void OnGunRefreshModifiers(Entity<GunFireModeSoundsComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes))
            return;

        if (ent.Comp.Sounds.TryGetValue(fireModes.CurrentFireMode, out var sound))
            args = args with { SoundGunshot = sound };
    }

    #endregion Starlight
}
