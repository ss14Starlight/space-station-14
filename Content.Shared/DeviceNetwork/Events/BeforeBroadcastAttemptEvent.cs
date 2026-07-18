using Content.Shared.DeviceNetwork.Components;

namespace Content.Shared.DeviceNetwork.Events;

/// <summary>
/// Sent to the sending entity before broadcasting network packets to recipients
/// </summary>
public sealed class BeforeBroadcastAttemptEvent : CancellableEntityEventArgs
{
    public readonly IReadOnlySet<Entity<DeviceNetworkComponent>> Recipients;
    public HashSet<Entity<DeviceNetworkComponent>>? ModifiedRecipients;

    public BeforeBroadcastAttemptEvent(IReadOnlySet<Entity<DeviceNetworkComponent>> recipients)
    {
        Recipients = recipients;
    }
}
