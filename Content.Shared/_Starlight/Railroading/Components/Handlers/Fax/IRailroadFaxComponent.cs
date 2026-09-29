namespace Content.Shared._Starlight.Railroading.Components.Handlers.Fax;

public interface IRailroadFaxComponent
{
    public HashSet<string> Addresses { get; }

    public List<RailroadFaxLetter> Letters { get; }
}
