namespace Content.Shared._NullLink;

public interface INullLinkPlayTimeManager
{
    TimeSpan GetPlayTime(string server, Guid player, string tracker);
}
