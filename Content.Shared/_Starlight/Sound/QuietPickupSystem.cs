using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Sound.Components;
using Content.Shared.Verbs;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Sound;

public sealed partial class QuietPickupSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly TimeSpan _delay = TimeSpan.FromSeconds(2.5);

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<EmitSoundOnPickupComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null ||
            args.Using != null ||
            !args.CanAccess ||
            !args.CanInteract ||
            !TryComp(ent, out ItemComponent? item) ||
            !item.AllowDirectHandPickup ||
            !_hands.CanPickupAnyHand(args.User, ent, handsComp: args.Hands, item: item))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("quiet-pickup-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Priority = -1,
            Act = () => StartPickup(user, ent),
        });
    }

    private void StartPickup(EntityUid user, EntityUid item)
        => _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, _delay, new QuietPickupDoAfterEvent(), item, target: item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });

    [SubscribeLocalEvent]
    private void OnDoAfter(Entity<EmitSoundOnPickupComponent> ent, ref QuietPickupDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        EnsureComp<SilentPickupComponent>(ent);
        _hands.TryPickupAnyHand(args.User, ent);
        RemComp<SilentPickupComponent>(ent);
    }
}

[Serializable, NetSerializable]
public sealed partial class QuietPickupDoAfterEvent : SimpleDoAfterEvent;
