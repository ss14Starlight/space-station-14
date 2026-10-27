// ReSharper disable CheckNamespace
namespace Content.Shared.DoAfter;

public sealed partial class DoAfterArgs
{
    // A cooperative controller binds a do-after to its own hand, not the body's global selection.
    [DataField] public string? ControlHand;
    [DataField] public NetEntity? HandController;

    private void CopyBodyControlFrom(DoAfterArgs other)
    {
        ControlHand = other.ControlHand;
        HandController = other.HandController;
    }
}
