using System.Linq;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnInitLanguageSpeaker);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentGetState>(OnGetLanguageState);
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, DetermineEntityLanguagesEvent>(OnDetermineUniversalLanguages);
        SubscribeNetworkEvent<LanguagesSetMessage>(OnClientSetLanguage);

        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, MapInitEvent>((uid, _, _) => UpdateEntityLanguages(uid));
        SubscribeLocalEvent<UniversalLanguageSpeakerComponent, ComponentRemove>((uid, _, _) => UpdateEntityLanguages(uid));
    }

    #region event handling

    private void OnInitLanguageSpeaker(Entity<LanguageSpeakerComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.CurrentLanguage))
            ent.Comp.CurrentLanguage = ent.Comp.SpokenLanguages.FirstOrDefault(UniversalPrototype);

        UpdateEntityLanguages(ent!);
    }

    private void OnGetLanguageState(Entity<LanguageSpeakerComponent> entity, ref ComponentGetState args)
    {
        args.State = new LanguageSpeakerComponent.State
        {
            CurrentLanguage = entity.Comp.CurrentLanguage,
            SpokenLanguages = entity.Comp.SpokenLanguages,
            UnderstoodLanguages = entity.Comp.UnderstoodLanguages
        };
    }

    private void OnDetermineUniversalLanguages(Entity<UniversalLanguageSpeakerComponent> entity, ref DetermineEntityLanguagesEvent ev)
    {
        ev.SpokenLanguages.Add(UniversalPrototype);
    }

    private void OnClientSetLanguage(LanguagesSetMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { Valid: true } uid)
            return;

        var language = GetLanguagePrototype(message.CurrentLanguage);
        if (language == null || !CanSpeak(uid, language.ID))
            return;

        SetLanguage(uid, language.ID);
    }

    /// <summary>
    /// Used to relay info via Language -> Radio, checks if Language also need to speak, hands, ect...
    /// </summary>
    /// <param name="source"></param>
    /// <param name="message"></param>
    /// <param name="channel"></param>
    /// <param name="language"></param>
    public void SendEntityRadioLanguage(EntityUid source, string message, ProtoId<RadioChannelPrototype> channel, LanguagePrototype language)
    {
        if (!_actionBlocker.CanSpeak(source) || (language.SpeechOverride.RequireHands && !_actionBlocker.CanInteract(source, null)))
            return;

        _radioSystem.SendRadioMessage(source, message, channel, source, language);
    }

    // Starlight-start: fix duplicate radio messages for language-override channels
    // This handler is ONLY for languages that reroute radio to a specific channel via
    // SpeechOverride.RadioChannel (e.g. a language that forces all speech onto a special freq).
    // The original guard was tautologically false — it checked (RadioChannel is null && Channel == RadioChannel),
    // which can never both be true, so the early-return never fired and every radio message was
    // sent twice: once by RadioSystem.OnIntrinsicReceive (correct) and once here (wrong).
    // Fix: return early unless all conditions are met — language has a RadioChannel override,
    // the incoming channel matches it, and the entity has an active player session.
    private void OnRadioReceiveEvent(EntityUid uid, LanguageKnowledgeComponent _, ref RadioReceiveEvent args)
    {
        if (args.Language.SpeechOverride.RadioChannel is null   // language doesn't use a radio override — not our job
            || args.Channel is null                              // no channel on the event (shouldn't happen, safety)
            || args.Channel != args.Language.SpeechOverride.RadioChannel // wrong channel for this language
            || !TryComp<ActorComponent>(uid, out var actor))    // entity has no player to send to
            return;

        _netMan.ServerSendMessage(new MsgChatMessage{ Message = args.OriginalChatMsg }, actor.PlayerSession.Channel);

        if (uid != args.MessageSource)
            args.Receivers.Add(uid);
    }
    // Starlight-end

    #endregion
}
