using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared._Starlight.Chat;
using Content.Shared._Starlight.Language; // Starlight

namespace Content.Server.Speech.EntitySystems;

/// <summary>
///     This system redirects local chat messages to listening entities (e.g., radio microphones).
/// </summary>
public sealed class ListeningSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly ChatSystem _chat = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpeak);
        SubscribeLocalEvent<EntityLoocEvent>(OnLooc); // Starlight
    }

    // Starlight edit Start
    private void OnSpeak(EntitySpokeEvent ev) =>
        PingListeners(ev.Source, ev.Message.Text, ev.IsWhisper, ev.Language);
    private void OnLooc(EntityLoocEvent ev) =>
        PingLoocListeners(ev.Source, ev.Message);
    // Starlight End

    public void PingListeners(EntityUid source, string message, bool isWhisper, LanguagePrototype? language = null) // Starlight
    {
        // TODO whispering / audio volume? Microphone sensitivity?
        // for now, whispering just arbitrarily reduces the listener's max range.

        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        var attemptEv = new ListenAttemptEvent(source);
        var ev = new ListenEvent(message, source, language);
        var obfuscatedEv = !isWhisper ? null : new ListenEvent(_chat.ObfuscateMessageReadability(message), source, language); // Starlight
        var query = EntityQueryEnumerator<ActiveListenerComponent, TransformComponent>();

        while(query.MoveNext(out var listenerUid, out var listener, out var xform))
        {
            if (xform.MapID != sourceXform.MapID)
                continue;

            // range checks
            // TODO proper speech occlusion
            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).LengthSquared();
            if (distance > listener.Range * listener.Range)
                continue;

            RaiseLocalEvent(listenerUid, attemptEv);
            if (attemptEv.Cancelled)
            {
                attemptEv.Uncancel();
                continue;
            }

            //Starlight begin
            var whisperClearRange = SharedChatSystem.WhisperClearRange;
            if(TryComp<ChatListenerRangeComponent>(source, out var rangeComp))
                whisperClearRange = rangeComp.WhisperClearRange;
            //Starlight end
            
            if (obfuscatedEv != null && distance > whisperClearRange) // Starlight-edit
                RaiseLocalEvent(listenerUid, obfuscatedEv);
            else
                RaiseLocalEvent(listenerUid, ev);
        }
    }
    // Starlight Start: Holopads support LOOC
    public void PingLoocListeners(EntityUid source, string message)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var sourceXform = xformQuery.GetComponent(source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform, xformQuery);

        var attemptEv = new ListenAttemptEvent(source);
        var ev = new LoocListenEvent(message, source);
        var query = EntityQueryEnumerator<ActiveListenerComponent, TransformComponent>();

        while(query.MoveNext(out var listenerUid, out var listener, out var xform))
        {
            if (xform.MapID != sourceXform.MapID)
                continue;

            var distance = (sourcePos - _xforms.GetWorldPosition(xform, xformQuery)).LengthSquared();
            if (distance > listener.Range * listener.Range)
                continue;

            RaiseLocalEvent(listenerUid, attemptEv);
            if (attemptEv.Cancelled)
            {
                attemptEv.Uncancel();
                continue;
            }

            RaiseLocalEvent(listenerUid, ev);
        }
    }
    // Starlight End
}
