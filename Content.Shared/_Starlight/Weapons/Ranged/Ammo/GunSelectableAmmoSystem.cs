using System.Runtime.InteropServices;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Weapons.Ranged.Ammo;

public sealed partial class GunSelectableAmmoSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunSelectableAmmoComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<GunSelectableAmmoComponent, GunSelectableAmmoActionEvent>(OnSelectAmmoAction);
        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanTraceEvent>(OnHitscanShot, before: [typeof(HitscanBasicRaycastSystem)]);
        SubscribeLocalEvent<GunSelectableAmmoComponent, GunSelectableAmmoSelectMessage>(OnRadialSelected);
        SubscribeLocalEvent<GunSelectableAmmoComponent, DroppedEvent>(OnDropped);
    }

    private void OnGetItemActions(Entity<GunSelectableAmmoComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnSelectAmmoAction(Entity<GunSelectableAmmoComponent> ent, ref GunSelectableAmmoActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<UserInterfaceComponent>(ent, out var uiComp))
            return;

        args.Handled = true;

        if (!_ui.IsUiOpen((ent, uiComp), GunSelectableAmmoUiKey.Key, args.Performer))
            _ui.OpenUi((ent, uiComp), GunSelectableAmmoUiKey.Key, args.Performer);
    }

    private void OnRadialSelected(Entity<GunSelectableAmmoComponent> ent, ref GunSelectableAmmoSelectMessage args)
    {
        if (!SelectAmmo(ent, args.Actor, args.SelectedIndex))
            return;
    }

    private void OnDropped(Entity<GunSelectableAmmoComponent> ent, ref DroppedEvent args) => _ui.CloseUi(ent.Owner, GunSelectableAmmoUiKey.Key, args.User);

    private bool SelectAmmo(Entity<GunSelectableAmmoComponent> ent, EntityUid user, int index)
    {
        if (ent.Comp.Settings.Count == 0)
            return false;

        if (index < 0 || index >= ent.Comp.Settings.Count)
            return false;

        ref var settingIndex = ref ent.Comp.Setting;
        settingIndex = index;

        var setting = ent.Comp.Settings[settingIndex];
        var popup = Loc.GetString("setting-ammunition-selected", ("ammo", Loc.GetString(setting.Name)));
        _popup.PopupClient(popup, user, user, PopupType.Medium);

        _audio.PlayPredicted(ent.Comp.SelectSound, ent, user);

        if (ent.Comp.Action is { } action)
            _actions.SetIcon(action, setting.Icon);

        Dirty(ent);
        return true;
    }

    private void OnHitscanShot(Entity<HitscanBasicDamageComponent> ent, ref HitscanTraceEvent args)
    {
        if (!TryComp<GunSelectableAmmoComponent>(args.Gun, out var selection) ||
            !TryComp<HitscanPierceComponent>(ent, out var pierce) ||
            !TryComp<HitscanRicochetComponent>(ent, out var ricochet))
            return;

        var settingIndex = selection.Setting;
        if (settingIndex < 0 || settingIndex >= selection.Settings.Count)
            return;

        ref var setting = ref CollectionsMarshal.AsSpan(selection.Settings)[settingIndex];

        ent.Comp.Damage = new DamageSpecifier(setting.Damage);
        ent.Comp.ArmorPenetration = setting.ArmorPiercing;

        ricochet.Chance = setting.RicochetChance;

        pierce.Chance = setting.PierceChance;
        pierce.Deviation = setting.PierceDeviation;
        pierce.PierceLevel = setting.PierceLevel;

        Dirty(ent, ent.Comp);
    }
}
