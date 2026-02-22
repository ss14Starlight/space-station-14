using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;

namespace Content.Server._Starlight.Fax;

public sealed partial class MinimalFaxInfo
{
    public string Content;
    public string Name;
    public string Sender;
    public List<StampDisplayInfo>? StampedBy = null;

    public MinimalFaxInfo(FaxPrintout prinout, string from)
    {
        Content = prinout.Content;
        Name = prinout.Name;
        Sender = from;
        StampedBy = prinout.StampedBy;
    }
}

[ByRefEvent]
public struct FaxRecievedEvent
{
    public MinimalFaxInfo Info;
}