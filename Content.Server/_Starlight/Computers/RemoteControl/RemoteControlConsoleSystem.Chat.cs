using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Chat;
using Content.Shared._Starlight.Computers.RemoteControl;
using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.GameObjects;
using static Content.Server.Chat.Systems.ChatSystem;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Computers.RemoteControl;

public sealed partial class RemoteControlConsoleSystem
{
    private void OnRemoteControllerTypingChanged(TypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var controller = args.SenderSession.AttachedEntity;
        if (controller is not { } controllerEntity)
            return;

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (!console.EnableRemoteView
                || console.Controller != controllerEntity
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || TerminatingOrDeleted(remoteEntity))
                continue;

            SetRemoteTypingState(remoteEntity, ev.State);
        }
    }

    private void SetRemoteTypingState(EntityUid remoteEntity, TypingIndicatorState state)
    {
        EnsureComp<AppearanceComponent>(remoteEntity);
        EnsureComp<TypingIndicatorComponent>(remoteEntity);
        _appearance.SetData(remoteEntity, TypingIndicatorVisuals.State, state);
    }

    [SubscribeLocalEvent]
    private void OnExpandRemoteControlChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        if (TerminatingOrDeleted(ev.Source))
            return;

        var sourceXform = Transform(ev.Source);

        foreach (var console in EntityQuery<RemoteControlConsoleComponent>())
        {
            if (!console.EnableRemoteView
                || !TryGetRemoteEntity(console, out var remoteEntity)
                || TerminatingOrDeleted(remoteEntity))
                continue;

            var remoteXform = Transform(remoteEntity);
            if (console.Controller is { } controller
                && _playerManager.TryGetSessionByEntity(controller, out var controllerSession)
                && sourceXform.MapID == remoteXform.MapID
                && sourceXform.Coordinates.TryDistance(EntityManager, remoteXform.Coordinates, out var controllerDistance)
                && WithinListenerRange(remoteEntity, ev.VoiceRange, controllerDistance, ev.IsWhisper))
                ev.Recipients.TryAdd(controllerSession, new ICChatRecipientData(controllerDistance, false, RemoteListener: remoteEntity));

            if (console.Controller != ev.Source)
                continue;

            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } listener
                    || listener == remoteEntity
                    || ev.Recipients.ContainsKey(session))
                    continue;

                var listenerXform = Transform(listener);
                if (listenerXform.MapID != remoteXform.MapID
                    || !remoteXform.Coordinates.TryDistance(EntityManager, listenerXform.Coordinates, out var distance)
                    || !WithinListenerRange(listener, ev.VoiceRange, distance, ev.IsWhisper))
                    continue;

                ev.Recipients.Add(session, new ICChatRecipientData(distance, false, RemoteSource: remoteEntity));
            }
        }
    }

    private bool WithinListenerRange(EntityUid listener, float baseRange, float distance, bool isWhisper)
    {
        if (!TryComp<ChatListenerRangeComponent>(listener, out var range)
            || !range.AllowExtendListenRange)
            return distance < baseRange;

        var extendedRange = isWhisper ? range.WhisperMuffledRange : range.VoiceRange;
        return distance < Math.Max(baseRange, extendedRange);
    }
}
