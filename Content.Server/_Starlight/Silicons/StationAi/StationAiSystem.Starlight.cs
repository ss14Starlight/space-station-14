using Content.Server._Starlight.Utility;
using Content.Server._Starlight.Utility.Events;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Power.Components;
using Content.Shared.Silicons.StationAi;

// ReSharper disable CheckNamespace
namespace Content.Server.Silicons.StationAi;

public sealed partial class StationAiSystem
{
    [Dependency] private DelayedEventSystem _delayedEvent = default!;

    private const string AiPowerLossAlertEventId = "station-ai-power-loss-alert";
    private static readonly TimeSpan _aiPowerLossAlertDelay = TimeSpan.FromSeconds(3);

    [SubscribeLocalEvent]
    private void OnDelayedEventTriggered(Entity<StationAiCoreComponent> ent, ref DelayedEventTriggeredEvent args)
    {
        if (args.EventId != AiPowerLossAlertEventId
            || !HasComp<ApcPowerReceiverBatteryComponent>(ent)
            || !TryGetHeld((ent.Owner, ent.Comp), out var held))
            return;

        var ev = new ChatNotificationEvent(_aiLosingPowerChatNotificationPrototype, ent.Owner);
        RaiseLocalEvent(held.Value, ref ev);
    }
}
