using System.Runtime.InteropServices;
using Content.Shared._Starlight.Weapons.Hitscan.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Weapons.Ranged.Ammo;

public sealed partial class GunToggleableAmmoSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunToggleableAmmoComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<GunToggleableAmmoComponent, GunToggleAmmoActionEvent>(OnToggleAmmoAction);
        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanTraceEvent>(OnHitscanShot, before: [typeof(HitscanBasicRaycastSystem)]);
    }

    private void OnGetItemActions(Entity<GunToggleableAmmoComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnToggleAmmoAction(Entity<GunToggleableAmmoComponent> ent, ref GunToggleAmmoActionEvent args)
    {
        if (ToggleAmmo(ent, args.Performer))
            args.Handled = true;
    }

    private void OnHitscanShot(Entity<HitscanBasicDamageComponent> ent, ref HitscanTraceEvent args)
    {
        if (!TryComp<GunToggleableAmmoComponent>(args.Gun, out var toggle) ||
            !TryComp<HitscanPierceComponent>(ent, out var pierce) ||
            !TryComp<HitscanRicochetComponent>(ent, out var ricochet))
            return;

        var settingIndex = toggle.Setting;
        if (settingIndex < 0 || settingIndex >= toggle.Settings.Count)
            return;

        ref var setting = ref CollectionsMarshal.AsSpan(toggle.Settings)[settingIndex];

        ent.Comp.Damage = new DamageSpecifier(setting.Damage);
        ent.Comp.ArmorPenetration = setting.ArmorPiercing;

        ricochet.Chance = setting.RicochetChance;

        pierce.Chance = setting.PierceChance;
        pierce.Deviation = setting.PierceDeviation;
        pierce.PierceLevel = setting.PierceLevel;

        Dirty(ent, ent.Comp);
        Log.Debug($"[Source (Hitscan)] Projectile UID: {ent}, Damage set: {ent.Comp.Damage}");
    }

    private bool ToggleAmmo(Entity<GunToggleableAmmoComponent> ent, EntityUid user)
    {
        if (ent.Comp.Settings.Count == 0)
            return false;

        ref var settingIndex = ref ent.Comp.Setting;
        settingIndex++;
        if (settingIndex >= ent.Comp.Settings.Count)
            settingIndex = 0;

        var setting = ent.Comp.Settings[settingIndex];
        var popup = Loc.GetString(ent.Comp.Message, ("ammo", Loc.GetString(setting.Name)));
        _popup.PopupClient(popup, user, user, PopupType.Large);

        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);

        if (ent.Comp.Action is { } action)
            _actions.SetIcon(action, setting.Icon);

        Dirty(ent);
        return true;
    }
}
