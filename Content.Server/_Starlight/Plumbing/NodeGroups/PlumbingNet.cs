using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;

namespace Content.Server._Starlight.Plumbing.NodeGroups;

public interface IPlumbingNet : INodeGroup
{
}

[NodeGroup(NodeGroupID.Plumbing)]
public sealed class PlumbingNet : BaseNodeGroup, IPlumbingNet
{
    public override string? GetDebugData()
    {
        return $"Nodes: {NodeCount}";
    }
}
