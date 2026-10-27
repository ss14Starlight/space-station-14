using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Weapons.Melee;

public abstract partial class SharedMeleeWeaponSystem
{
    private EntityUid? GetAttackUser(EntitySessionEventArgs args)
        => args.SenderSession.AttachedEntity is { } actor
            ? EntityManager.System<BodyControlHandContextSystem>().GetControlledBody(actor) : null;

    private bool WithBodyControl(EntitySessionEventArgs args, Action command)
        => args.SenderSession.AttachedEntity is { } actor
           && EntityManager.System<BodyControlHandContextSystem>()
               .WithController(actor, "body.interact", HasComp<HandsComponent>(GetAttackUser(args)), command);
}
