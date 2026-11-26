using System.Diagnostics.CodeAnalysis;
using Content.Shared._Starlight.Radio;
using Content.Shared.StatusIcon;

//ReSharper disable once CheckNamespace
namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    private bool TryGetRadioJobIconOverride(EntityUid messageSource, [NotNullWhen(true)] out JobIconPrototype? icon)
    {
        icon = null;

        var ev = new GetRadioJobIconEvent();
        RaiseLocalEvent(messageSource, ev);

        return ev.JobIcon is { } id && _prototype.TryIndex(id, out icon);
    }
}
