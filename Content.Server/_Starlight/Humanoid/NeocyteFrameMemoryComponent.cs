using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Humanoid;

/// <summary>
/// Carries a Neocyte's selected frame through polymorphs.
/// </summary>
[RegisterComponent]
public sealed partial class NeocyteFrameMemoryComponent : Component
{
    [ViewVariables]
    public EntProtoId? FramePrototype;
}
