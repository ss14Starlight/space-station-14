using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.Prayer;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Starlight.Magic.Events;
using Robust.Server.Console;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Magic.Systems;

/// <summary>
///     Implementation for the Elf 'Psychic Whisper' Cantrip
/// </summary>
public sealed class PsychicWhisperSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly PrayerSystem _prayerSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateActionsComponent, PsychicWhisperEvent>(OnPsychicWhisper);
    }

    private void OnPsychicWhisper(EntityUid uid, MobStateActionsComponent component, PsychicWhisperEvent ev)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        // is this even valid?
        if (!TryComp(ev.Performer, out ActorComponent? performerActor))
            return;
        if (!TryComp(ev.Target, out ActorComponent? targetActor))
            return;

        _quickDialog.OpenDialog(performerActor.PlayerSession, Loc.GetString("action-name-psychic-whisper"), "",
            (string message) =>
            {
                // make sure no one died/DC'd while you were typing:

                if (EntityManager.GetComponentOrNull<ActorComponent>(ev.Performer) is not {PlayerSession: var performerPlayerSession} ||
                    EntityManager.GetComponentOrNull<ActorComponent>(ev.Target) is not {PlayerSession: var targetPlayerSession})
                {
                    return;
                }

                // if a person is gibbed/deleted, no psychic whisper for you!
                if (Deleted(uid))
                    return;

                // Intentionally does not check for muteness, must be alive
                if (actor.PlayerSession.AttachedEntity != uid || !_mobState.IsAlive(uid))
                    return;

                // _chat.TrySendInGameICMessage(uid, lastWords, InGameICChatType.Whisper, ChatTransmitRange.Normal, checkRadioPrefix: false, ignoreActionBlocker: true);
                _prayerSystem.SendSubtleMessage(targetPlayerSession, performerPlayerSession, message, Loc.GetString("prayer-popup-subtle-psychic-whisper"));
            });

        ev.Handled = true;
    }
}
