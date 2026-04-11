using System.Diagnostics.CodeAnalysis;
using Content.Shared.Charges.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.UserInterface;

namespace Content.Shared._Starlight.EventSelector;

public abstract class SharedEventSelectorSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected const string _delayId = "EventSelectorId";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EventSelectorRadialMenuComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
    }

    private void OnUIOpenAttempt(Entity<EventSelectorRadialMenuComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (CanActivate(ent, out var popupString))
            return;

        args.Cancel();

        if (!args.Silent)
            _popup.PopupClient(popupString, args.User, args.User);
    }

    /// <summary>
    /// Is the given event radial menu available to select?
    /// </summary>
    /// <param name="ent">The entity your checking</param>
    /// <param name="popupString">The popup message you should use for if it got canceled.</param>
    /// <returns>True if you can activate the entity, false if you can't (E.g on cooldown or out of charges)</returns>
    public bool CanActivate(Entity<EventSelectorRadialMenuComponent> ent, [NotNullWhen(false)] out string? popupString)
    {
        popupString = null;

        if (_useDelay.IsDelayed(ent.Owner, _delayId))
        {
            popupString = Loc.GetString("syndicate-disruptor-cooldown");
            return false;
        }

        if (_charges.IsEmpty(ent.Owner))
        {
            popupString = Loc.GetString("syndicate-disruptor-no-charge");
            return false;
        }

        return true;
    }
}
