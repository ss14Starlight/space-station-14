using Robust.Client.UserInterface.RichText;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Meta tag that marks a section of paperwork as containing metadata. This is used by the fax system
/// to find what area to override when sending (forwarding) a fax that already had metadata printed on it.
/// </summary>
public sealed class MetaTag : IMarkupTagHandler
{
    public string Name => "meta";
}
