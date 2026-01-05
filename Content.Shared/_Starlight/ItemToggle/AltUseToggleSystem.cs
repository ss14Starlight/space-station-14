using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared._Starlight.ItemToggle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Localization;

namespace Content.Shared._Starlight.ItemToggle;

/// <summary>
/// A simple system to add alt verb action to toggle system
/// </summary>
public sealed class AltUseToggleSystem : EntitySystem
{
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AltUseToggleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<AltUseToggleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp<ItemToggleComponent>(ent, out var itemToggle))
            return;

        var user = args.User;

        if (itemToggle.Activated)
        {
            var ev = new ItemToggleDeactivateAttemptEvent(args.User);
            RaiseLocalEvent(ent.Owner, ref ev);

            if (ev.Cancelled)
                return;
        }
        else
        {
            var ev = new ItemToggleActivateAttemptEvent(args.User);
            RaiseLocalEvent(ent.Owner, ref ev);

            if (ev.Cancelled)
                return;
        }

        args.Verbs.Add(new AlternativeVerb()
        {
            Text = !itemToggle.Activated ? _loc.GetString(itemToggle.VerbToggleOn) : _loc.GetString(itemToggle.VerbToggleOff),
            Act = () =>
            {
                _itemToggle.Toggle((ent.Owner, itemToggle), user, predicted: itemToggle.Predictable);
            }
        });
    }
}
