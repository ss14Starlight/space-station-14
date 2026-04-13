using Content.Shared.Fax.Components;
using Content.Shared.Paper;

namespace Content.Server._Starlight.Fax;

public sealed partial class MinimalFaxInfo
{
    public string Content;
    public string Name;
    public string Sender;
    public List<StampDisplayInfo>? StampedBy = null;

    public MinimalFaxInfo(FaxPrintout printout, string from)
    {
        Content = printout.Content;
        Name = printout.Name;
        Sender = from;
        StampedBy = printout.StampedBy is null
            ? null
            : [.. printout.StampedBy];
    }
}

[ByRefEvent]
public struct FaxReceivedEvent
{
    public MinimalFaxInfo Info;
}
