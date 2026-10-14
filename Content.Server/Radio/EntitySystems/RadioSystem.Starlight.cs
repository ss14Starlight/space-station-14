using Content.Shared._Starlight.Language;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Player;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;


    private bool SendRadioMessageSL(EntityUid messageSource, RadioChannelPrototype channel, ref LanguagePrototype? language)
    {
        if (channel.AutoTranslate is not null)
            language = _language.GetLanguagePrototype(channel.AutoTranslate.Value);

        language ??= _language.GetLanguage(messageSource);

        if ((!language.Speech.AllowRadio && language.Speech.RadioChannel is not null && language.Speech.RadioChannel != channel)
            || (!language.Speech.AllowRadio && language.Speech.RadioChannel is null))
            return true;

        if (!_mobState.IsSoftCritical(messageSource)) return false;
        if (_playerManager.TryGetSessionByEntity(messageSource, out var session))
            _popup.PopupEntity(Loc.GetString("radio-failed-to-send-soft-critical"), messageSource, session, PopupType.Medium);
        return true;

    }
}
