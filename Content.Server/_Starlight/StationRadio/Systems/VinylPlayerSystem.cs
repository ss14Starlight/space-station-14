using Content.Server.Station.Systems;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared._Starlight.StationRadio.Systems;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._Starlight.StationRadio.Systems;

public sealed partial class VinylPlayerSystem : SharedVinylPlayerSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationRadioReceiverSystem _stationRadio = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VinylPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Playing)
                continue;

            if (!comp.Standalone && !_stationRadio.TryGetLinkedPoweredServer(uid, out _))
            {
                _popup.PopupEntity(Loc.GetString("vinyl-popout-no-radio-connection"), uid, PopupType.Medium);
                Pause((uid, comp));
                continue;
            }

            if (Timing.CurTime >= comp.PlaybackEnd)
                Finish((uid, comp));
        }
    }

    protected override void OnPowerChanged(Entity<VinylPlayerComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            Pause(ent);
    }

    protected override void OnDestruction(Entity<VinylPlayerComponent> ent, ref DestructionEventArgs args)
        => Stop(ent);

    protected override void DiscInserted(Entity<VinylPlayerComponent> ent, int slot)
    {
        Changed(ent);

        if (ent.Comp.AutoPlay && !ent.Comp.Playing && ent.Comp.CurrentSlot == slot)
            Play(ent);
    }

    protected override void DiscRemoved(Entity<VinylPlayerComponent> ent, bool wasCurrent)
    {
        if (wasCurrent)
            Stop(ent);
        else
            Changed(ent);
    }

    public override void TogglePlay(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Playing)
            Pause(ent);
        else
            Play(ent, user);
    }

    /// <summary>
    /// Plays the current disc from where it was paused.
    /// </summary>
    public bool Play(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
        var (uid, comp) = ent;
        if (comp.Playing
            || GetCurrentDisc(ent) is not { } disc
            || !TryComp<VinylComponent>(disc, out var vinyl)
            || vinyl.Song == null)
            return false;

        if (!_power.IsPowered(uid))
        {
            Popup(uid, user, "vinyl-popout-no-power");
            return false;
        }

        EntityUid server = default;
        if (!comp.Standalone)
        {
            if (_station.GetOwningStation(uid) == null)
            {
                Popup(uid, user, "vinyl-popout-no-station");
                return false;
            }

            if (!_stationRadio.TryGetLinkedPoweredServer(uid, out server))
            {
                Popup(uid, user, "vinyl-popout-no-radio-connection");
                return false;
            }
        }

        var length = _audio.GetAudioLength(_audio.ResolveSound(vinyl.Song));
        if (comp.Position >= length)
            comp.Position = TimeSpan.Zero;

        var start = Timing.CurTime - comp.Position;
        if (comp.Standalone)
        {
            if (_audio.PlayPvs(vinyl.Song, uid, comp.DefaultParams) is { } audio)
            {
                _audio.SetPlaybackPosition((audio.Entity, audio.Component), (float) comp.Position.TotalSeconds);
                comp.SoundEntity = audio.Entity;
            }
        }
        else if (TryComp<StationRadioServerComponent>(server, out var serverComp))
        {
            _stationRadio.PlayTrack((server, serverComp), uid, vinyl.Song, start);
        }

        comp.Playing = true;
        comp.PlaybackStart = start;
        comp.PlaybackEnd = start + length;
        Dirty(ent);
        Changed(ent);
        return true;
    }

    /// <summary>
    /// Stops playing and remembers where, so <see cref="Play"/> continues from there.
    /// </summary>
    public void Pause(Entity<VinylPlayerComponent> ent)
    {
        if (!ent.Comp.Playing)
            return;

        ent.Comp.Position = Timing.CurTime - ent.Comp.PlaybackStart;
        StopOutput(ent);
        Changed(ent);
    }

    /// <summary>
    /// Goes back to the start of the current disc, still playing if it was.
    /// </summary>
    public override void Rewind(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
        var wasPlaying = ent.Comp.Playing;
        StopOutput(ent);
        ent.Comp.Position = TimeSpan.Zero;

        if (!wasPlaying || !Play(ent, user))
            Changed(ent);
    }

    /// <summary>
    /// Skips to the next disc, wrapping around after the last one. Keeps playing if it was.
    /// </summary>
    public void Next(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
        if (NextSlot(ent, wrap: true) is { } next)
            SelectSlot(ent, next, user);
    }

    /// <summary>
    /// Switches to the disc in the given slot from its start. Keeps playing if it was.
    /// </summary>
    public void SelectSlot(Entity<VinylPlayerComponent> ent, int slot, EntityUid? user = null)
    {
        if (slot < 0 || slot >= ent.Comp.Slots.Length || GetDisc(ent, slot) == null)
            return;

        var wasPlaying = ent.Comp.Playing;
        StopOutput(ent);
        ent.Comp.CurrentSlot = slot;
        ent.Comp.Position = TimeSpan.Zero;
        Dirty(ent);
        UpdateVisuals(ent);

        if (!wasPlaying || !Play(ent, user))
            Changed(ent);
    }

    /// <summary>
    /// Ejects the disc in the given slot, into the user's hands if they stand at the player.
    /// </summary>
    public void EjectSlot(Entity<VinylPlayerComponent> ent, int slot, EntityUid? user = null)
    {
        if (slot < 0 || slot >= ent.Comp.Slots.Length
            || !_itemSlots.TryEject(ent, ent.Comp.Slots[slot], user, out var disc))
            return;

        if (user != null && _interaction.InRangeUnobstructed(user.Value, ent.Owner))
            _hands.TryPickupAnyHand(user.Value, disc.Value);
    }

    /// <summary>
    /// How long the current disc is, zero without a playable disc.
    /// </summary>
    public TimeSpan GetLength(Entity<VinylPlayerComponent> ent)
        => !TryComp<VinylComponent>(GetCurrentDisc(ent), out var vinyl) || vinyl.Song == null
            ? TimeSpan.Zero
            : _audio.GetAudioLength(_audio.ResolveSound(vinyl.Song));

    /// <summary>
    /// Stops playing and goes back to the start of the disc.
    /// </summary>
    public void Stop(Entity<VinylPlayerComponent> ent)
    {
        StopOutput(ent);
        ent.Comp.Position = TimeSpan.Zero;
        Changed(ent);
    }

    private void Finish(Entity<VinylPlayerComponent> ent)
    {
        var disc = GetCurrentDisc(ent);
        Stop(ent);

        if (disc != null)
        {
            var ev = new VinylFinishedEvent(ent);
            RaiseLocalEvent(disc.Value, ref ev);
        }

        if (TerminatingOrDeleted(ent) || !ent.Comp.AutoAdvance || NextSlot(ent) is not { } next)
            return;

        ent.Comp.CurrentSlot = next;
        Dirty(ent);
        UpdateVisuals(ent);
        Play(ent);
    }

    private int? NextSlot(Entity<VinylPlayerComponent> ent, bool wrap = false)
    {
        var count = ent.Comp.Slots.Length;
        var steps = ent.Comp.Loop || wrap ? count : count - 1 - ent.Comp.CurrentSlot;
        for (var i = 1; i <= steps; i++)
        {
            var slot = (ent.Comp.CurrentSlot + i) % count;
            if (TryComp<VinylComponent>(GetDisc(ent, slot), out var vinyl) && vinyl.Song != null)
                return slot;
        }

        return null;
    }

    private void StopOutput(Entity<VinylPlayerComponent> ent)
    {
        if (ent.Comp.SoundEntity != null)
            ent.Comp.SoundEntity = _audio.Stop(ent.Comp.SoundEntity);

        _stationRadio.StopTrack(ent);

        if (!ent.Comp.Playing)
            return;

        ent.Comp.Playing = false;
        Dirty(ent);
    }

    private void Popup(EntityUid uid, EntityUid? user, string message)
    {
        if (user != null)
            _popup.PopupEntity(Loc.GetString(message), uid, user.Value, PopupType.Medium);
        else
            _popup.PopupEntity(Loc.GetString(message), uid, PopupType.Medium);
    }

    private void Changed(Entity<VinylPlayerComponent> ent)
    {
        var ev = new VinylPlayerStateChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}
