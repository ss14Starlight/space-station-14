using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Starlight.Paper;

public sealed partial class PenPokeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!; // Sends the prick message only to the target.

    [Dependency] private SharedAudioSystem _audio = default!; // Plays the click for nearby clients and predicts it for the user.

    [Dependency] private UseDelaySystem _useDelay = default!; // Reuses the cooldown configured on the pen prototype.

    private static readonly SoundSpecifier PokeSound = new SoundPathSpecifier(
        "/Audio/Items/pen_click.ogg",
        AudioParams.Default.WithVolume(-4f).WithMaxDistance(2f)); // Match the regular pen's sound, volume, and audible range.

    /// <inheritdoc />
    public override void Initialize() // Subscribe pen interactions to the poke handler.
    {
        base.Initialize();
        SubscribeLocalEvent<PenPokeComponent, AfterInteractEvent>(OnAfterInteract);
    }

    /// <summary>
    /// Gives a reachable character prick feedback when a pen is used on them.
    /// </summary>
    /// <param name="pen">The pen used for the interaction.</param>
    /// <param name="args">The interaction details and handled state.</param>
    private void OnAfterInteract(Entity<PenPokeComponent> pen, ref AfterInteractEvent args) // Handle pen clicks on entities.
    {
        if (args.Handled
            || !args.CanReach // Do not poke targets that are out of reach.
            || args.Target is not { Valid: true } target // Ignore floor clicks and invalid or deleted targets.
            || args.User == target // You can't poke yourself. Duh.
            || HasComp<InjectorComponent>(pen) // Injector pens already have their own interactions with poking.
            || !HasComp<BloodstreamComponent>(target)) // Only characters with a bloodstream receive this interaction. No poking plants, walls, or other inanimate objects.
            return;

        if (!TryComp<UseDelayComponent>(pen, out var useDelay)
            || !_useDelay.TryResetDelay((pen.Owner, useDelay), checkDelayed: true)) // Start the pen's configured cooldown if it is ready.
        {
            args.Handled = true; // Keep cooldown clicks from falling through to unrelated interactions.
            return;
        }

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target); // Show the hypospray-style message only to the target.
        _audio.PlayPredicted(PokeSound, pen.Owner, args.User); // Play the click for the user and nearby listeners.
        args.Handled = true; // Prevent lower-priority interactions from also processing this poke.
    }
}
