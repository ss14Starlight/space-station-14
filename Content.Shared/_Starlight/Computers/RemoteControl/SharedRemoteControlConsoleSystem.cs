using Content.Shared.Body.Organ;
using Content.Shared.Interaction;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Starlight.Computers.RemoteControl;

public sealed partial class SharedRemoteControlConsoleSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

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

    private void OnRemoteControlInteractionCheck(ref RemoteControlInteractionCheckEvent args)
    {
        if (!TryGetControlledEntity(args.Actor, out var remoteEntity))
            return;

        args.RemoteEntity = remoteEntity;
        args.Allowed = _interaction.InRangeAndAccessible(remoteEntity, args.Target);
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
