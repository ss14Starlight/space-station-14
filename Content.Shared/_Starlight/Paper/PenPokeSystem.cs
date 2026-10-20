using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Shared._Starlight.Paper;

/// <summary>
/// Handles the harmless prick feedback when a pen is used on another character.
/// </summary>
public sealed partial class PenPokeSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    /// <summary>
    /// Subscribes the pen to interactions so character targets receive the prick popup.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PenPokeComponent, AfterInteractEvent>(OnAfterInteract);
    }

    /// <summary>
    /// Shows the target-only prick popup for a reachable character, leaving injector behavior unchanged.
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

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target);
        args.Handled = true;
    }
}
