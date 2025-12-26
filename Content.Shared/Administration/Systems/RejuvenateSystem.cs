using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.Administration.Systems;

public sealed class RejuvenateSystem : EntitySystem
{
    // starlight start - add instant action handler for rejuvenate action
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private SoundPathSpecifier Sound = new("/Audio/Magic/staff_change.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, RejuvenateInstantActionEvent>(OnRejuvenateInstantEvent);
    }

    private void OnRejuvenateInstantEvent(Entity<ActionsComponent> ent, ref RejuvenateInstantActionEvent args)
    {
        PerformRejuvenate(args.Performer);
        _popup.PopupPredicted(Loc.GetString("entity-rejuvenated-popup", ("name", Name(args.Performer))), args.Performer, args.Performer, PopupType.LargeCaution);
        _audio.PlayPredicted(Sound, args.Performer, args.Performer);
        args.Handled = true;
    }
    // starlight end

    /// <summary>
    /// Fully heals the target, removing all damage, debuffs or other negative status effects.
    /// </summary>
    public void PerformRejuvenate(EntityUid target)
    {
        RaiseLocalEvent(target, new RejuvenateEvent());
    }
}

// starlight start
/// <summary>
/// Instant action to rejuvenate self
/// </summary>
public sealed partial class RejuvenateInstantActionEvent : InstantActionEvent { };
// starlight end