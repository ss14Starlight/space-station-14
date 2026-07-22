namespace Content.Shared._Starlight.Access;
[ByRefEvent]
public record struct AccessReadEvent()
{
    public bool Denied = false;
}
