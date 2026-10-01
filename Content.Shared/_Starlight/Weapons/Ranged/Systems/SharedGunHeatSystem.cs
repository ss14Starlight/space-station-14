using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Weapons.Ranged.Systems;

public abstract partial class SharedGunHeatSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    /// <summary>
    /// How strongly the barrel glows at the given temperature, from 0 to 1.
    /// </summary>
    public static float GetGlow(GunHeatComponent comp, float temperature)
    {
        var range = comp.MeltTemperature - comp.GlowTemperature;
        return range <= 0f ? 0f : Math.Clamp((temperature - comp.GlowTemperature) / range, 0f, 1f);
    }

    public static float GetJamChance(GunHeatComponent comp, float temperature)
    {
        var range = comp.MeltTemperature - comp.JamTemperature;
        if (temperature < comp.JamTemperature || range <= 0f)
            return 0f;

        return comp.MaxJamChance * Math.Clamp((temperature - comp.JamTemperature) / range, 0f, 1f);
    }

    [SubscribeLocalEvent]
    private void OnAttemptShoot(Entity<GunHeatComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.Jammed)
            return;

        args.Cancelled = true;

        if (Timing.CurTime < ent.Comp.NextPopupTime)
            return;

        ent.Comp.NextPopupTime = Timing.CurTime + ent.Comp.PopupCooldown;
        Popup.PopupClient(Loc.GetString("gun-heat-jammed-blocked"), ent, args.User, PopupType.SmallCaution);
    }

    [SubscribeLocalEvent(before: [typeof(SharedWieldableSystem), typeof(SharedGunSystem), typeof(BatteryWeaponFireModesSystem)])]
    private void OnUseInHand(Entity<GunHeatComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !ent.Comp.Jammed)
            return;

        args.Handled = true;
        ent.Comp.Jammed = false;
        Dirty(ent);

        Audio.PlayPredicted(ent.Comp.UnjamSound, ent, args.User);
        Popup.PopupClient(Loc.GetString("gun-heat-unjammed"), ent, args.User);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<GunHeatComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Jammed)
            args.PushMarkup(Loc.GetString("gun-heat-examine-jammed"));

        var temperature = ent.Comp.Temperature;
        if (temperature >= ent.Comp.JamTemperature + ((ent.Comp.MeltTemperature - ent.Comp.JamTemperature) * 0.5f))
            args.PushMarkup(Loc.GetString("gun-heat-examine-critical"));
        else if (temperature >= ent.Comp.JamTemperature)
            args.PushMarkup(Loc.GetString("gun-heat-examine-overheated"));
        else if (temperature >= ent.Comp.GlowTemperature)
            args.PushMarkup(Loc.GetString("gun-heat-examine-hot"));
        else if (temperature >= ent.Comp.GlowTemperature - 30f)
            args.PushMarkup(Loc.GetString("gun-heat-examine-warm"));
    }
}
