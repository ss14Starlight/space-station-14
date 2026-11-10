using Robust.Shared.GameStates;

// ReSharper disable CheckNamespace
namespace Content.Shared.Bed.Sleep;

public sealed partial class SleepingComponent
{
    [AutoNetworkedField]
    public bool WasKnockedDownBeforeSleep;
}
