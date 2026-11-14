// ReSharper disable CheckNamespace

namespace Content.Shared._Starlight.Access;

[ByRefEvent]
public record struct AccessReadEvent()
{
    public bool Denied = false;

    /// <summary>
    /// Entity whose access credentials replace those of the event owner.
    /// </summary>
    public EntityUid? AccessSource;
}
