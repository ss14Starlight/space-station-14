using Content.Server.Station.Systems;
using Content.Shared._Starlight.StationRadio.Events;
using Content.Shared._Starlight.StationRadio.Systems;
using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Verbs;
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
    [Dependency] private IGameTiming _timing = default!;

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

            if (_timing.CurTime >= comp.PlaybackEnd)
                Finish((uid, comp));
        }
    }

    protected override void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (!args.Powered)
            Pause((uid, comp));
    }

    protected override void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
        => Stop((uid, comp));

    protected override void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        var slot = Array.IndexOf(comp.Slots, args.Container.ID);
        if (slot < 0)
            return;

        var current = GetCurrentDisc((uid, comp));
        if (!comp.Playing && (current == null || current == args.Entity))
        {
            comp.CurrentSlot = slot;
            comp.Position = TimeSpan.Zero;
            Dirty(uid, comp);
        }

        UpdateVisuals((uid, comp));
        Changed((uid, comp));

        if (comp.AutoPlay && !comp.Playing && comp.CurrentSlot == slot)
            Play((uid, comp));
    }

    protected override void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        var slot = Array.IndexOf(comp.Slots, args.Container.ID);
        if (slot < 0)
            return;

        if (slot == comp.CurrentSlot)
            Stop((uid, comp));

        UpdateVisuals((uid, comp));
        Changed((uid, comp));
    }

    [SubscribeLocalEvent]
    private void OnActivate(EntityUid uid, VinylPlayerComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !comp.ToggleOnActivate)
            return;

        TogglePlay((uid, comp), args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(EntityUid uid, VinylPlayerComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetCurrentDisc((uid, comp)) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(comp.Playing ? "vinyl-player-verb-pause" : "vinyl-player-verb-play"),
            Priority = 1,
            Act = () => TogglePlay((uid, comp), user),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("vinyl-player-verb-rewind"),
            Act = () => Rewind((uid, comp), user),
        });
    }

    public void TogglePlay(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
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

        var start = _timing.CurTime - comp.Position;
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

        ent.Comp.Position = _timing.CurTime - ent.Comp.PlaybackStart;
        StopOutput(ent);
        Changed(ent);
    }

    /// <summary>
    /// Goes back to the start of the current disc, still playing if it was.
    /// </summary>
    public void Rewind(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
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
