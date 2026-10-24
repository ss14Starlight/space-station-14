using Content.Shared._Starlight.Language;
using Robust.Shared.Player;

namespace Content.Server._Starlight.TextToSpeech;

public sealed class ExpandTtsRecipientsEvent(EntityUid source, float range, LanguagePrototype language, Filter recipients) : EntityEventArgs
{
    public readonly EntityUid Source = source;
    public readonly float Range = range;
    public readonly LanguagePrototype Language = language;
    public readonly Filter Recipients = recipients;
}
