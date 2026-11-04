using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.Components;

namespace Content.Server._Starlight.Xenoarchaeology.Artifact;

/// <summary>
/// Logs each artifact node a player sets off: who did it, on whom, and what the node does.
/// </summary>
public sealed partial class XenoArtifactActivationLogSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoArtifactNodeComponent, XenoArtifactNodeActivatedEvent>(OnNodeActivated);
    }

    private void OnNodeActivated(Entity<XenoArtifactNodeComponent> node, ref XenoArtifactNodeActivatedEvent args)
    {
        // Nodes fired by unlocking have no user; upstream's node log covers those.
        if (args.User is not { } user)
            return;

        var artifact = args.Artifact.Owner;
        var effect = DescribeEffect(node);

        if (user == artifact)
        {
            _adminLogger.Add(LogType.ArtifactNode, LogImpact.Medium,
                $"{ToPrettyString(artifact):user} self-activated at {args.Coordinates}: {effect:effect}");
        }
        else if (args.Target is { } target && target != artifact && target != user)
        {
            _adminLogger.Add(LogType.ArtifactNode, LogImpact.Medium,
                $"{ToPrettyString(user):user} activated {ToPrettyString(artifact):artifact} on {ToPrettyString(target):target} at {args.Coordinates}: {effect:effect}");
        }
        else
        {
            _adminLogger.Add(LogType.ArtifactNode, LogImpact.Medium,
                $"{ToPrettyString(user):user} activated {ToPrettyString(artifact):artifact} at {args.Coordinates}: {effect:effect}");
        }
    }

    // Prototype ids name most effects, but foam and puddle nodes can pick their chemicals at random.
    private string DescribeEffect(EntityUid node)
    {
        var effect = Prototype(node)?.ID ?? Name(node);

        if (TryComp<XAEFoamComponent>(node, out var foam) && foam.SelectedReagent != null)
            return $"{effect} ({foam.SelectedReagent} foam)";

        if (TryComp<XAECreatePuddleComponent>(node, out var puddle) && puddle.SelectedChemicals is { Count: > 0 } chemicals)
            return $"{effect} ({string.Join(", ", chemicals)} puddle)";

        return effect;
    }
}
