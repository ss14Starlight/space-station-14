using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Paper;

/// <summary>
/// Handles the harmless prick feedback when a pen is used on another character.
/// </summary>
public sealed partial class PenPokeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    private static readonly SoundSpecifier PokeSound = new SoundPathSpecifier(
        "/Audio/Items/pen_click.ogg",
        AudioParams.Default.WithVolume(-4f).WithMaxDistance(2f));

    /// <summary>
    /// Subscribes the pen to interactions so character targets receive the prick popup.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PenPokeComponent, AfterInteractEvent>(OnAfterInteract);
    }

    /// <summary>
    /// Shows prick feedback, plays the pen click, and starts the pen's use delay for a reachable character target.
    /// </summary>
    private void OnAfterInteract(Entity<PenPokeComponent> pen, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { Valid: true } target
            || args.User == target
            || HasComp<InjectorComponent>(pen)
            || !HasComp<BloodstreamComponent>(target))
            return;

        if (!TryComp<UseDelayComponent>(pen, out var useDelay)
            || !_useDelay.TryResetDelay((pen.Owner, useDelay), checkDelayed: true))
        {
            args.Handled = true;
            return;
        }

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target);
        _audio.PlayPredicted(PokeSound, pen.Owner, args.User);
        args.Handled = true;
    }
}
