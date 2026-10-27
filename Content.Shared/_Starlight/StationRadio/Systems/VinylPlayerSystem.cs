using Content.Shared._Starlight.StationRadio.Components;
using Content.Shared.Destructible;
using Content.Shared.Power;
using Robust.Shared.Containers;
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

    [SubscribeLocalEvent]
    protected virtual void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
    }

    [SubscribeLocalEvent]
    protected virtual void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
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
    private void OnExamined(EntityUid uid, VinylPlayerComponent comp, ref ExaminedEvent args)
    {
        if (GetCurrentDisc((uid, comp)) is not { } vinyl)
        {
            args.PushMarkup(Loc.GetString("vinyl-player-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("vinyl-player-examine-loaded", ("vinyl", Name(vinyl))));
        args.PushMarkup(Loc.GetString(comp.Playing ? "vinyl-player-examine-playing" : "vinyl-player-examine-paused"));
    }
}
