using Content.Server.Xenoarchaeology.Artifact.XAE.Components;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Access.Components;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE;

/// <summary>
/// System for xeno artifact activation effect that gives artifacts accesses.
/// </summary>
public sealed partial class XAEGainAccessSystem : BaseXAESystem<XAEGainAccessComponent>
{
    [Dependency] private TagSystem _tag = default!;
    /// <inheritdoc />
    protected override void OnActivated(Entity<XAEGainAccessComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        // Give the artifact an AccessComponent if it lacks one, else get the existing AccessComponent
        var component = EnsureComp<AccessComponent>(args.Artifact);
        var beforeLength = component.Tags.Count;
        component.Tags.UnionWith(ent.Comp.Accesses);
        _tag.AddTag(args.Artifact, ent.Comp.DoorBumpTag);
        if (beforeLength != component.Tags.Count)
        {
            Dirty(args.Artifact, component);
        }
    }
}
