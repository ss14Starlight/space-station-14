using Content.Server.Chat.Systems;
using Content.Shared._Functional.TutorialServer;
using Content.Shared.Chat;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._Functional.TutorialServer;

/// <summary>
/// Activates HoP-line visitors when the matching sub-goal is current: speak + drop an ID on the desk.
/// </summary>
public sealed partial class TutorialHoPQueueSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TutorialServerRuleSystem _tutorial = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var visitors = EntityQueryEnumerator<TutorialHoPVisitorComponent, TransformComponent>();
        while (visitors.MoveNext(out var visitorUid, out var visitor, out var visitorXform))
        {
            if (visitor.Activated)
                continue;

            if (TryComp<MobStateComponent>(visitorUid, out var mobState) &&
                mobState.CurrentState is MobState.Dead or MobState.Critical)
                continue;

            var mapUid = visitorXform.MapUid;
            if (mapUid == null)
                continue;

            var participants = EntityQueryEnumerator<TutorialParticipantComponent, TransformComponent>();
            while (participants.MoveNext(out var playerUid, out var part, out var playerXform))
            {
                if (playerXform.MapUid != mapUid)
                    continue;

                if (!_tutorial.TryGetCurrentSubGoal(playerUid, part, out var sub))
                    continue;

                if (!string.Equals(sub.Id, visitor.ActivateOnSubGoal, StringComparison.Ordinal))
                    continue;

                ActivateVisitor(visitorUid, visitor);
                break;
            }
        }
    }

    private void ActivateVisitor(EntityUid visitorUid, TutorialHoPVisitorComponent visitor)
    {
        visitor.Activated = true;
        Dirty(visitorUid, visitor);

        var message = Loc.GetString(visitor.Dialogue);
        _chat.TrySendInGameICMessage(visitorUid, message, InGameICChatType.Speak, hideChat: false, hideLog: true);

        var dropCoords = _transform.GetMoverCoordinates(visitorUid).Offset(visitor.DeskDropOffset);
        Spawn(visitor.IdCardProto, dropCoords);
    }
}
