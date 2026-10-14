using Content.Shared.Access;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Xenoarchaeology.Artifact.XAE.Components;

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

    /// <summary>
    /// The access groups the artifact will have.
    /// </summary>
    [DataField] public HashSet<ProtoId<AccessGroupPrototype>> AccessGroups = new();

    /// <summary>
    /// Tag applied to the artifact so it can open doors matching its granted accesses when it bumps into them.
    /// </summary>
    [DataField] public ProtoId<TagPrototype> DoorBumpTag = "DoorBumpOpener";
}

