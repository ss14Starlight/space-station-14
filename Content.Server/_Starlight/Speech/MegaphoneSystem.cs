using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._Starlight.Megaphone;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Player;

namespace Content.Server._Starlight.Speech;

public sealed partial class MegaphoneSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private const int MaxMessageLength = 150;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCMegaphoneComponent, MegaphoneInputRequestedEvent>(OnMegaphoneInput);
    }

    private void OnMegaphoneInput(Entity<RMCMegaphoneComponent> megaphone, ref MegaphoneInputRequestedEvent args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !TryComp<SpeechComponent>(args.User, out _) ||
            !_hands.IsHolding(args.User, megaphone.Owner))
            return;

        var user = args.User;
        var session = actor.PlayerSession;
        _quickDialog.OpenDialog<LongString>(
            session,
            Loc.GetString("megaphone-input-title"),
            Loc.GetString("megaphone-input-prompt"),
            message =>
            {
                if (string.IsNullOrWhiteSpace(message.String) ||
                    !TryComp<RMCMegaphoneComponent>(megaphone.Owner, out var currentMegaphone) ||
                    !_hands.IsHolding(user, megaphone.Owner) ||
                    !TryComp<SpeechComponent>(user, out var speech))
                    return;

                if (message.String.Length > MaxMessageLength)
                {
                    _popup.PopupEntity(Loc.GetString("megaphone-input-too-long"), user, user, PopupType.SmallCaution);
                    return;
                }

                var originalSpeechVerb = speech.SpeechVerb;
                var originalSpeechSounds = speech.SpeechSounds;
                var originalSuffixSpeechVerbs = speech.SuffixSpeechVerbs;

                speech.SpeechVerb = currentMegaphone.SpeechVerb;
                speech.SpeechSounds = currentMegaphone.MegaphoneSpeechSound;
                speech.SuffixSpeechVerbs = new(currentMegaphone.SuffixSpeechVerbs);
                Dirty(user, speech);

                try
                {
                    _chat.TrySendInGameICMessage(user, message.String, InGameICChatType.Speak, false, player: session);
                }
                finally
                {
                    speech.SpeechVerb = originalSpeechVerb;
                    speech.SpeechSounds = originalSpeechSounds;
                    speech.SuffixSpeechVerbs = originalSuffixSpeechVerbs;
                    Dirty(user, speech);
                }
            });
    }
}
