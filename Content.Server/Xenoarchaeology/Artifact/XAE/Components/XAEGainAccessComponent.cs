using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// XenoArtifact effect that grant accesses to artifacts.
/// </summary>
[RegisterComponent, Access(typeof(XAEGainAccessSystem))]
public sealed partial class XAEGainAccessComponent : Component
{
    /// <summary>
    /// The accesses the artifact will have.
    /// </summary>
    [DataField] public HashSet<ProtoId<AccessLevelPrototype>> Accesses = new();
}

