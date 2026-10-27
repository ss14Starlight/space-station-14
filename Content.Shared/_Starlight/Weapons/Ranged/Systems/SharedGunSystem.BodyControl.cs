using Content.Shared._Starlight.BodyControl;
using Content.Shared.Hands.Components;

// ReSharper disable CheckNamespace
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private EntityUid? GetShootingUser(EntitySessionEventArgs args)
        => args.SenderSession.AttachedEntity is { } actor
            ? EntityManager.System<BodyControlHandContextSystem>().GetControlledBody(actor) : null;

    private bool WithBodyControl(EntitySessionEventArgs args, Action command)
        => args.SenderSession.AttachedEntity is { } actor
           && EntityManager.System<BodyControlHandContextSystem>()
               .WithController(actor, "body.interact", HasComp<HandsComponent>(GetShootingUser(args)), command);

    private bool CanUseBothHands(EntityUid body)
        => !EntityManager.System<BodyControlHandContextSystem>().TryGetContext(body, out var context)
           || context.Access.FullControl || context.Access.Allows("body.hands") && context.Access.Hands == null && context.Access.Parts == null;
}
