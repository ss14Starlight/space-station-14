using Content.Shared.ActionBlocker;
using Content.Shared.Body.Organ;
using Content.Shared.Interaction;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Computers.RemoteControl;

public sealed partial class SharedRemoteControlConsoleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RemoteControlInteractionCheckEvent>(OnRemoteControlInteractionCheck);
    }

    /// <summary>
    /// Gets the remote entity controlled by an actor.
    /// </summary>
    /// <param name="actor">The controlling entity.</param>
    /// <param name="remoteEntity">The remote entity, if the actor controls one.</param>
    /// <returns>True if the actor controls a remote entity.</returns>
    public bool TryGetControlledEntity(EntityUid actor, out EntityUid remoteEntity)
    {
        if (TryComp<RemoteControlControllerComponent>(actor, out var controller))
        {
            foreach (var consoleUid in controller.Consoles)
            {
                if (!TryComp<RemoteControlConsoleComponent>(consoleUid, out var console)
                    || console.Controller != actor
                    || !TryGetRemoteEntity(console, out remoteEntity))
                    continue;

                return true;
            }
        }

        remoteEntity = default;
        return false;
    }

    /// <summary>
    /// Checks whether the controlled remote entity is eligible to perform conscious actions.
    /// </summary>
    /// <remarks>
    /// This checks the remote entity through <c>ConsciousAttemptEvent</c> and does not check whether the controller
    /// can act.
    /// </remarks>
    /// <param name="remoteEntity">The controlled remote entity whose conscious-action eligibility is checked.</param>
    /// <returns>
    /// <see langword="true"/> if the remote entity can perform conscious actions; otherwise, <see langword="false"/>.
    /// </returns>
    public bool CanRemoteControlledEntityAct(EntityUid remoteEntity)
        => _actionBlocker.CanConsciouslyPerformAction(remoteEntity);

    private void OnRemoteControlInteractionCheck(ref RemoteControlInteractionCheckEvent args)
    {
        if (!TryGetControlledEntity(args.Actor, out var remoteEntity))
            return;

        args.RemoteEntity = remoteEntity;
        args.Allowed = CanRemoteControlledEntityAct(remoteEntity)
            && _interaction.InRangeAndAccessible(remoteEntity, args.Target);
    }

    public bool TryGetRemoteEntity(Entity<RemoteControlConsoleComponent?> entity, out EntityUid remoteEntity)
    {
        if (!Resolve(entity, ref entity.Comp))
        {
            remoteEntity = default;
            return false;
        }

        return TryGetRemoteEntity(entity.Comp, out remoteEntity);
    }

    private bool TryGetRemoteEntity(RemoteControlConsoleComponent component, out EntityUid remoteEntity)
    {
        remoteEntity = default;
        if (component.RemoteBrain is not { } brain)
            return false;

        if (TryGetBody(component, out var body))
        {
            remoteEntity = body;
            return true;
        }

        remoteEntity = brain;
        return true;
    }

    public bool TryGetBody(Entity<RemoteControlConsoleComponent?> entity, out EntityUid body)
    {
        if (!Resolve(entity, ref entity.Comp))
        {
            body = default;
            return false;
        }

        return TryGetBody(entity.Comp, out body);
    }

    private bool TryGetBody(RemoteControlConsoleComponent component, out EntityUid body)
    {
        body = default;
        if (component.RemoteBrain is not { } brain)
            return false;

        if (TryComp<OrganComponent>(brain, out var organ) && organ.Body is { } organBody)
        {
            body = organBody;
            return true;
        }

        return _containers.TryGetContainingContainer(brain, out var container)
            && container.ID == "borg_brain"
            && TryComp<BorgChassisComponent>(container.Owner, out _)
            && (body = container.Owner) != default;
    }
}
