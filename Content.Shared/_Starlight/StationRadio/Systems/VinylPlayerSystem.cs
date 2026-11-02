using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Destructible;
using Content.Shared.Power;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Shared._Starlight.StationRadio.Systems;

public abstract partial class SharedVinylPlayerSystem : EntitySystem
{
    /// <summary>
    /// Container holding the disc being played.
    /// </summary>
    public const string VinylContainer = "vinyl";

    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] protected IGameTiming Timing = default!;

    [SubscribeLocalEvent]
    protected virtual void OnPowerChanged(Entity<VinylPlayerComponent> ent, ref PowerChangedEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnDestruction(Entity<VinylPlayerComponent> ent, ref DestructionEventArgs args)
    {
    }

    [SubscribeLocalEvent]
    private void OnVinylInserted(Entity<VinylPlayerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var slot = Array.IndexOf(ent.Comp.Slots, args.Container.ID);
        if (slot < 0 || Timing.ApplyingState)
            return;

        var current = GetCurrentDisc(ent);
        if (!ent.Comp.Playing && (current == null || current == args.Entity))
        {
            ent.Comp.CurrentSlot = slot;
            ent.Comp.Position = TimeSpan.Zero;
            Dirty(ent);
        }

        UpdateVisuals(ent);
        DiscInserted(ent, slot);
    }

    [SubscribeLocalEvent]
    private void OnVinylRemove(Entity<VinylPlayerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        var slot = Array.IndexOf(ent.Comp.Slots, args.Container.ID);
        if (slot < 0 || Timing.ApplyingState)
            return;

        var wasCurrent = slot == ent.Comp.CurrentSlot;
        if (wasCurrent && FilledSlotFrom(ent, slot) is { } next)
        {
            ent.Comp.CurrentSlot = next;
            ent.Comp.Position = TimeSpan.Zero;
            Dirty(ent);
        }

        UpdateVisuals(ent);
        DiscRemoved(ent, wasCurrent);
    }

    protected virtual void DiscInserted(Entity<VinylPlayerComponent> ent, int slot)
    {
    }

    protected virtual void DiscRemoved(Entity<VinylPlayerComponent> ent, bool wasCurrent)
    {
    }

    private int? FilledSlotFrom(Entity<VinylPlayerComponent> ent, int start)
    {
        var count = ent.Comp.Slots.Length;
        for (var i = 1; i <= count; i++)
        {
            var slot = (start + i) % count;
            if (GetDisc(ent, slot) != null)
                return slot;
        }

        return null;
    }

    [SubscribeLocalEvent]
    private void OnActivate(Entity<VinylPlayerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !ent.Comp.ToggleOnActivate)
            return;

        TogglePlay(ent, args.User);
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<VinylPlayerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetCurrentDisc(ent) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Playing ? "vinyl-player-verb-pause" : "vinyl-player-verb-play"),
            Priority = -1,
            Act = () => TogglePlay(ent, user),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("vinyl-player-verb-rewind"),
            Priority = -2,
            Act = () => Rewind(ent, user),
        });
    }

    /// <summary>
    /// Starts or pauses the current disc. Playback runs on the server only.
    /// </summary>
    public virtual void TogglePlay(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
    }

    /// <summary>
    /// Goes back to the start of the current disc. Playback runs on the server only.
    /// </summary>
    public virtual void Rewind(Entity<VinylPlayerComponent> ent, EntityUid? user = null)
    {
    }

    /// <summary>
    /// The disc in the given slot of the player, if any.
    /// </summary>
    public EntityUid? GetDisc(Entity<VinylPlayerComponent> ent, int slot)
    {
        if (slot < 0 || slot >= ent.Comp.Slots.Length
            || !Container.TryGetContainer(ent, ent.Comp.Slots[slot], out var container))
            return null;

        return container.ContainedEntities.Count > 0 ? container.ContainedEntities[0] : null;
    }

    /// <summary>
    /// The disc being played or cued.
    /// </summary>
    public EntityUid? GetCurrentDisc(Entity<VinylPlayerComponent> ent) => GetDisc(ent, ent.Comp.CurrentSlot);

    /// <summary>
    /// Shows the current disc on the player, tinted with its label color.
    /// </summary>
    protected void UpdateVisuals(Entity<VinylPlayerComponent> ent)
    {
        var disc = GetCurrentDisc(ent);
        _appearance.SetData(ent, VinylPlayerVisuals.Disc, disc != null);

        var mask = 0;
        for (var i = 0; i < ent.Comp.Slots.Length; i++)
        {
            if (GetDisc(ent, i) != null)
                mask |= 1 << i;
        }

        _appearance.SetData(ent, VinylPlayerVisuals.SlotMask, mask);
        if (TryComp<VinylComponent>(disc, out var vinyl))
            _appearance.SetData(ent, VinylPlayerVisuals.LabelColor, vinyl.LabelColor);
    }

    /// <summary>
    /// Show what vinyl is currently inserted when examined.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnExamined(Entity<VinylPlayerComponent> ent, ref ExaminedEvent args)
    {
        if (GetCurrentDisc(ent) is not { } vinyl)
        {
            args.PushMarkup(Loc.GetString("vinyl-player-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("vinyl-player-examine-loaded", ("vinyl", Name(vinyl))));
        args.PushMarkup(Loc.GetString(ent.Comp.Playing ? "vinyl-player-examine-playing" : "vinyl-player-examine-paused"));
    }
}
