using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared._Starlight.Atmos;
using Content.Shared.Atmos;
using Content.Shared.NodeContainer;
using Robust.Shared.Map;

namespace Content.Server._Starlight.Atmos.EntitySystems;

/// <summary>
/// Handles <see cref="PipePrefillComponent"/>: fills the entity's pipe segment with gas once, then removes the component.
/// </summary>
public sealed partial class PipePrefillSystem : EntitySystem
{
    [Dependency] private NodeGroupSystem _nodeGroup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PipePrefillComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PipePrefillComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
    }

    private void OnMapInit(Entity<PipePrefillComponent> ent, ref MapInitEvent args) =>
        PrefillPipeSegment(ent);

    private void OnAnchorStateChanged(Entity<PipePrefillComponent> ent, ref AnchorStateChangedEvent args) =>
        PrefillPipeSegment(ent);

    private void PrefillPipeSegment(Entity<PipePrefillComponent> ent)
    {
        var xform = Transform(ent);
        if (!xform.Anchored) return; // Not anchored = no pipenet
        if (xform.MapID == MapId.Nullspace) return;
        if (LifeStage(ent) < EntityLifeStage.MapInitialized) return;
        RemComp<PipePrefillComponent>(ent);

        // If we don't even have a node that *could* be part of a pipenet, just quit.
        if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
            return;

        var fractionTotal = 0f;
        foreach (var fraction in ent.Comp.Mixture.Values)
            fractionTotal += fraction;
        if (fractionTotal <= 0)
            return;

        // No target? Do nothing.
        if (!ent.Comp.TargetPressure.HasValue && !ent.Comp.TargetMoles.HasValue)
            return;

        // Does this run for every node on an entity? Yes. Is that bad? Probably not. For example, if we would use this
        // on a gas mixer, that'd be both inlets and the outlet. No big deal if you ask me. (Though why you would want
        // to use this on multi-node entities is beyond me).
        foreach (var node in nodeContainer.Nodes.Values)
        {
            if (node is not PipeNode pipeNode)
                continue;

            // Give this node a PipeNet immediately (no-op if it already has one) so its Air mixture
            // actually exists before we mutate it, without forcing a full update of every other
            // queued node group on the map. It'll get properly merged with any connected neighbours,
            // gas and all, next time the node group system processes its queue.
            _nodeGroup.CreateSingleNetImmediate(pipeNode);

            // Given the target pressure + our volume, calculate how many mols that would take to achieve.
            var targetMoles = 0f;
            if (ent.Comp.TargetMoles.HasValue)
                targetMoles = ent.Comp.TargetMoles.Value;
            if (ent.Comp.TargetPressure.HasValue)
                targetMoles = ent.Comp.TargetPressure.Value * pipeNode.Volume / (Atmospherics.R * ent.Comp.Temperature);

            // Add one gas at a time, proportional to the relative mixture.
            var air = pipeNode.Air;
            foreach (var (gas, fraction) in ent.Comp.Mixture)
                air.AdjustMoles(gas, targetMoles * fraction / fractionTotal);
        }
    }

}
