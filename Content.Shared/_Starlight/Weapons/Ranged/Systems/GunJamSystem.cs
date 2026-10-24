using Content.Shared._Starlight.Weapons.Ranged.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Starlight.Weapons.Ranged.Systems;

// Handles per-shot jam chance for guns with GunJamDefectComponent.
// A jammed gun cannot fire until the player racks the slide (Z / Use In Hand).
public sealed partial class GunJamSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    /// <summary>
    /// Salt for the random number generator to ensure that the jam chance is different each time a shot is fired.
    /// </summary>
    private const int JamSalt = -4;


    [SubscribeLocalEvent]
    private void OnAttemptShoot(Entity<GunJamDefectComponent> ent, ref AttemptShootEvent args)
    {
        if (!ent.Comp.IsJammed)
            return;

        args.Cancelled = true;

        // Rate-limit the popup to avoid flooding the screen when the player holds the trigger.
        if (_timing.CurTime < ent.Comp.NextPopupTime)
            return;

        ent.Comp.NextPopupTime = _timing.CurTime + ent.Comp.PopupCooldown;
        _popup.PopupClient(Loc.GetString("gun-jam-blocked"), ent, args.User, PopupType.SmallCaution);
    }

    [SubscribeLocalEvent]
    private void OnGunShot(Entity<GunJamDefectComponent> ent, ref GunShotEvent args)
    {
        if (ent.Comp.IsJammed)
            return;

        if (!_gun.GetShotRandom(ent, JamSalt).Prob(ent.Comp.JamChance))
            return;

        ent.Comp.IsJammed = true;
        Dirty(ent, ent.Comp);

        _audio.PlayPredicted(ent.Comp.SoundJamRack, ent.Owner, args.User);
        _popup.PopupClient(Loc.GetString("gun-jam-jammed"), ent, args.User, PopupType.MediumCaution);
        ent.Comp.NextPopupTime = _timing.CurTime + ent.Comp.PopupCooldown;
    }

    [SubscribeLocalEvent]
    private void OnUseInHand(Entity<GunJamDefectComponent> ent, ref UseInHandEvent args)
    {
        if (!ent.Comp.IsJammed)
            return;

        ent.Comp.IsJammed = false;
        Dirty(ent, ent.Comp);

        _popup.PopupClient(Loc.GetString("gun-jam-cleared"), ent, args.User);
    }
}
