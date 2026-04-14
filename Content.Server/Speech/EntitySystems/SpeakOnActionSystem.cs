using Content.Server.Chat.Systems;
using Content.Shared._Starlight.Speech;
using Content.Shared.Actions.Events;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Speech.Muting;

namespace Content.Server.Speech.EntitySystems;

/// <summary>
/// As soon as the chat refactor moves to Shared
/// the logic here can move to the shared <see cref="SharedSpeakOnActionSystem"/>
/// </summary>
public sealed class SpeakOnActionSystem : SharedSpeakOnActionSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeakOnActionComponent, ActionPerformedEvent>(OnActionPerformed);
    }

    private void OnActionPerformed(Entity<SpeakOnActionComponent> ent, ref ActionPerformedEvent args)
    {
        var user = args.Performer;

        // If we can't speak, we can't speak
        if (!HasComp<SpeechComponent>(user) || HasComp<MutedComponent>(user))
            return;

        if (string.IsNullOrWhiteSpace(ent.Comp.Sentence))
            return;

        // Starlight start
        var message = new SpeechMessage
        {
            Text = Loc.GetString(ent.Comp.Sentence),
            Tts = ent.Comp.Tts.HasValue ? Loc.GetString(ent.Comp.Tts) : Loc.GetString(ent.Comp.Sentence),
            Modifier = ent.Comp.Modifier,
        };

        _chat.TrySendInGameICMessage(user, message, ent.Comp.Whisper ? InGameICChatType.Whisper : InGameICChatType.Speak, false);
        // Starlight end
    }
}
