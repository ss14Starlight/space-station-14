using Content.Shared.Tag;

// ReSharper disable once CheckNamespace
namespace Content.Server.Xenoarchaeology.Equipment.Systems;

public sealed partial class ArtifactCrusherSystem
{
    [Dependency] private TagSystem _tagSystem = default!;
    private const string XenoBorgCrusherTag = "XenoBorgCrusher";
    private const string XenoBorgBag = "XenoborgStorageBag";

}
