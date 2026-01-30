
namespace Content.Shared.VendingMachines
{
    /// <summary>
    /// Indicates that a given vending machine should not get stocked on MapInit (e.g. when constructed)
    /// </summary>
    [RegisterComponent]
    public sealed partial class EmptyVendingMachineComponent : Component
    {
    }
}
