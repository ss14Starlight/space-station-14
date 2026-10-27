using Content.Shared._Starlight.BodyControl;
using Robust.Shared.Player;

namespace Content.Server._Starlight.BodyControl;

public sealed partial class BodyControlSystem
{
    /// <summary>Checks ownership without changing the provider's movement or takeover policy.</summary>
    public bool HasConflictingControl(EntityUid actor, EntityUid body, EntityUid? provider = null)
    {
        if (TryComp<BodyControlSourceComponent>(actor, out var source)
            && (!source.ExternallyDriven || source.Provider != provider || source.Body != body))
            return true;
        if (actor != body && HasComp<BodyControlTargetComponent>(actor))
            return true;
        if (TryComp<BodyControlSourceComponent>(body, out var bodySource)
            && (!bodySource.ExternallyDriven || bodySource.Provider != provider || bodySource.Body != body))
            return true;
        if (!TryComp<BodyControlTargetComponent>(body, out var target))
            return false;
        return !target.ExternallyDriven || target.ControllerCount != 1 || target.SoleController != actor
               || source == null || source.Provider != provider;
    }

    /// <summary>Reserves exclusive ownership. The provider retains its existing relay, UI and body-specific setup.</summary>
    public bool TryStartExclusiveControl(EntityUid actor, EntityUid body, EntityUid provider)
    {
        if (TerminatingOrDeleted(actor) || TerminatingOrDeleted(body) || TerminatingOrDeleted(provider)
            || !TryComp<ActorComponent>(actor, out var player) || player.PlayerSession.AttachedEntity != actor
            || HasComp<ActorComponent>(body) || HasConflictingControl(actor, body, provider))
            return false;
        if (TryComp<BodyControlSourceComponent>(actor, out var existing))
            return existing.ExternallyDriven && existing.Provider == provider && existing.Body == body;

        var target = new BodyControlTargetComponent
        {
            ExternallyDriven = true,
            ControllerCount = 1,
            SoleController = actor,
        };
        target.Controllers.Add(actor);
        AddComp(body, target);
        AddComp(actor, new BodyControlSourceComponent
        {
            Body = body,
            Provider = provider,
            ExternallyDriven = true,
            Access = new BodyControlAccess { FullControl = true },
        });
        return true;
    }

    public void StopExclusiveControl(EntityUid actor, EntityUid provider)
        => ReleaseExclusiveControl(actor, provider, notifyProvider: false);

    private void ReleaseExclusiveControl(EntityUid actor, EntityUid? provider, bool notifyProvider)
    {
        if (!TryComp<BodyControlSourceComponent>(actor, out var source)
            || !source.ExternallyDriven || source.Provider != provider || !source.Body.IsValid())
            return;
        var body = source.Body;
        source.Body = EntityUid.Invalid;
        source.Provider = null;
        if (source.LifeStage <= ComponentLifeStage.Running)
            RemComp<BodyControlSourceComponent>(actor);
        if (TryComp<BodyControlTargetComponent>(body, out var target) && target.ExternallyDriven)
        {
            target.Controllers.Remove(actor);
            target.ControllerCount = target.Controllers.Count;
            target.SoleController = null;
            if (target.LifeStage <= ComponentLifeStage.Running)
            {
                Dirty(body, target);
                if (target.Controllers.Count == 0)
                    RemComp<BodyControlTargetComponent>(body);
            }
        }
        if (notifyProvider && provider is { } owner && !TerminatingOrDeleted(owner))
        {
            var released = new BodyControlProviderReleasedEvent(actor, body);
            RaiseLocalEvent(owner, ref released);
        }
    }
}
