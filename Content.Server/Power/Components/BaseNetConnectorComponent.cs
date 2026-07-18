using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.NodeContainer;
using Content.Shared.NodeContainer.NodeGroups;
using Content.Shared.Power;

namespace Content.Server.Power.Components
{
    // TODO find a way to just remove this or turn it into one component.
    // Component interface queries require enumerating over ALL of an entities components.
    // So BaseNetConnectorNodeGroup<TNetType> is slow as shit.
    public interface IBaseNetConnectorComponent<in TNetType>
    {
        void SetNet(EntityUid uid, TNetType? net);
        Voltage Voltage { get; }
        string? NodeId { get; }
    }

    public abstract partial class BaseNetConnectorComponent<TNetType> : Component, IBaseNetConnectorComponent<TNetType>
        where TNetType : class
    {
        [Dependency] private IEntityManager _entMan = default!;

        [ViewVariables]
        public Voltage Voltage => _voltage;
        [DataField("voltage")]
        private Voltage _voltage = Voltage.High;

        [ViewVariables]
        public TNetType? Net => _net;
        private TNetType? _net;

        [ViewVariables] public bool NeedsNet => _net != null;

        [DataField("node")] public string? NodeId { get; set; }

        public void TryFindAndSetNet(EntityUid uid)
        {
            if (TryFindNet(uid, out var net))
            {
                SetNet(uid, net);
            }
        }

        public void ClearNet(EntityUid uid)
        {
            if (_net != null)
            {
                RemoveSelfFromNet(uid, _net);
                _net = null;
            }
        }

        protected abstract void AddSelfToNet(EntityUid uid, TNetType net);

        protected abstract void RemoveSelfFromNet(EntityUid uid, TNetType net);

        private bool TryFindNet(EntityUid uid, [NotNullWhen(true)] out TNetType? foundNet)
        {
            if (_entMan.TryGetComponent(uid, out NodeContainerComponent? container))
            {
                var compatibleNet = container.Nodes.Values
                    .Where(node => (NodeId == null || NodeId == node.Name) && node.NodeGroupID == (NodeGroupID) Voltage)
                    .Select(node => node.NodeGroup)
                    .OfType<TNetType>()
                    .FirstOrDefault();

                if (compatibleNet != null)
                {
                    foundNet = compatibleNet;
                    return true;
                }
            }
            foundNet = default;
            return false;
        }

        public void SetNet(EntityUid uid, TNetType? newNet)
        {
            if (_net != null)
                RemoveSelfFromNet(uid, _net);

            if (newNet != null)
                AddSelfToNet(uid, newNet);

            _net = newNet;
        }

        public void SetVoltage(EntityUid uid, Voltage newVoltage)
        {
            ClearNet(uid);
            _voltage = newVoltage;
            TryFindAndSetNet(uid);
        }
    }
}
