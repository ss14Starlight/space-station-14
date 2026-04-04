using System.Linq;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Starlight.Language;
using Content.Shared._Starlight.Language.Components;
using Content.Shared._Starlight.Language.Events;
using Content.Shared._Starlight.Language.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Language;

public sealed partial class LanguageSystem : SharedLanguageSystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LanguageSpeakerComponent, MapInitEvent>(OnInitLanguageSpeaker);
        SubscribeLocalEvent<LanguageSpeakerComponent, ComponentGetState>(OnGetLanguageState);
        SubscribeLocalEvent<LanguageKnowledgeComponent, RadioReceiveEvent>(OnRadioReceiveEvent);
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

    private void OnRadioReceiveEvent(EntityUid uid, LanguageKnowledgeComponent _, ref RadioReceiveEvent args)
    {
        if ((args.Language.SpeechOverride.RadioChannel is null && args.Channel is not null && args.Channel == args.Language.SpeechOverride.RadioChannel)|| !TryComp<ActorComponent>(uid, out var actor))
            return;

        _netMan.ServerSendMessage(new MsgChatMessage{ Message = args.OriginalChatMsg }, actor.PlayerSession.Channel);

        if (uid != args.MessageSource)
            args.Receivers.Add(uid);
    }

    #endregion
}