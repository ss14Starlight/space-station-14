using Content.Server.Chat.Systems;
using Content.Server.Popups; // Starlight
using Content.Shared.Abilities.Mime; // Starlight
using Content.Shared.Chat;
using Content.Shared.DeltaV.AACTablet;
using Content.Shared.DeltaV.QuickPhrase;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeltaV.AACTablet;

public sealed partial class AACTabletSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!; // Starlight Edit: Timing -> _timing. protected -> private
    [Dependency] private PopupSystem _popupSystem = default!; // Starlight

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AACTabletComponent, AACTabletSendPhraseMessage>(OnSendPhrase);
    }

    private void OnSendPhrase(EntityUid uid, AACTabletComponent component, AACTabletSendPhraseMessage message)
    {
        #region Starlight
        // prevent use of the AAC tablet while a mime's vow of silence is active
        if (TryComp<MimePowersComponent>(message.Actor, out var mime) && !mime.VowBroken)
        {
            _popupSystem.PopupEntity(Loc.GetString("mime-cant-speak"), message.Actor, message.Actor);
            return;
        }
        #endregion Starlight

        if (component.NextPhrase > _timing.CurTime) // Starlight Edit: Timing -> _timing
            return;

        // the AAC tablet uses the name of the person who pressed the tablet button
        // for quality of life
        var senderName = Identity.Entity(message.Actor, EntityManager);
        var speakerName = Loc.GetString("speech-name-relay",
            ("speaker", Name(uid)),
            ("originalName", senderName));

        if (!_prototypeManager.TryIndex<QuickPhrasePrototype>(message.PhraseID, out var phrase))
            return;

        _chat.TrySendInGameICMessage(uid,
            _loc.GetString(phrase.Text),
            InGameICChatType.Speak,
            hideChat: false,
            nameOverride: speakerName);

        var curTime = _timing.CurTime; // Starlight Edit: Timing -> _timing
        component.NextPhrase = curTime + component.Cooldown;
    }
}
