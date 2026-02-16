using Content.Server._Starlight.Plumbing.NodeGroups;
using Content.Server.NodeContainer.Nodes;

namespace Content.Server._Starlight.Plumbing.Nodes;

/// <summary>
///     A pipe node for plumbing systems using reagents instead of gases.
///     Extends PipeNode to reuse all pipe connection logic (direction, color, layer matching).
///     The only difference is it uses PlumbingNet instead of PipeNet.
/// </summary>
[DataDefinition]
[Virtual]
public partial class PlumbingNode : PipeNode
{
    /// <summary>
    ///     The <see cref="IPlumbingNet"/> this plumbing duct is part of.
    /// </summary>
    [ViewVariables]
    public IPlumbingNet? PlumbingNet => (IPlumbingNet?) NodeGroup;
}
