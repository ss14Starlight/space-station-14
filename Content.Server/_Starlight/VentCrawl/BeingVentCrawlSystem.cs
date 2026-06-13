using Content.Server.Ghost;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Content.Shared.NodeContainer;
using Content.Server._Starlight.Medical.Body.Systems;
using Content.Shared._Starlight.VentCrawl.Components;

namespace Content.Server.VentCrawl;

public sealed partial class BeingVentCrawlSystem : EntitySystem
{
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private GhostSystem _ghost = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeingVentCrawlComponent, InhaleLocationEvent>(OnInhaleLocation);
        SubscribeLocalEvent<BeingVentCrawlComponent, ExhaleLocationEvent>(OnExhaleLocation);
        SubscribeLocalEvent<BeingVentCrawlComponent, AtmosExposedGetAirEvent>(OnGetAir);
        SubscribeLocalEvent<BeingVentCrawlComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, BeingVentCrawlComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState != MobState.Critical)
            return;

        if (TryComp<ActorComponent>(uid, out var actor))
        {
            var session = actor.PlayerSession;

            if (!_mindSystem.TryGetMind(session, out var mindId, out var mind))
            {
                mindId = _mindSystem.CreateMind(session.UserId);
                mind = Comp<MindComponent>(mindId);
            }

            _ghost.OnGhostAttempt(mindId, true, true, true, mind);
        }
    }

    private void OnGetAir(EntityUid uid, BeingVentCrawlComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (!TryComp<VentCrawlHolderComponent>(component.Holder, out var holder))
            return;

        if (holder.CurrentTube == null)
            return;

        if (!TryComp(holder.CurrentTube.Value, out NodeContainerComponent? nodeContainer))
            return;
        foreach (var nodeContainerNode in nodeContainer.Nodes)
        {
            if (!_nodeContainer.TryGetNode(nodeContainer, nodeContainerNode.Key, out PipeNode? pipe))
                continue;
            args.Gas = pipe.Air;
            args.Handled = true;
            return;
        }
    }

    private void OnInhaleLocation(EntityUid uid, BeingVentCrawlComponent component, InhaleLocationEvent args)
    {
        if (!TryComp<VentCrawlHolderComponent>(component.Holder, out var holder))
            return;

        if (holder.CurrentTube == null)
            return;

        if (!TryComp(holder.CurrentTube.Value, out NodeContainerComponent? nodeContainer))
            return;
        foreach (var nodeContainerNode in nodeContainer.Nodes)
        {
            if (!_nodeContainer.TryGetNode(nodeContainer, nodeContainerNode.Key, out PipeNode? pipe))
                continue;
            args.Gas = pipe.Air;
            return;
        }
    }

    private void OnExhaleLocation(EntityUid uid, BeingVentCrawlComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<VentCrawlHolderComponent>(component.Holder, out var holder))
            return;

        if (holder.CurrentTube == null)
            return;

        if (!TryComp(holder.CurrentTube.Value, out NodeContainerComponent? nodeContainer))
            return;
        foreach (var nodeContainerNode in nodeContainer.Nodes)
        {
            if (!_nodeContainer.TryGetNode(nodeContainer, nodeContainerNode.Key, out PipeNode? pipe))
                continue;
            args.Gas = pipe.Air;
            return;
        }
    }
}
