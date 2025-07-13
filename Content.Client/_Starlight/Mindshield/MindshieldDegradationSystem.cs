using Content.Shared._Starlight.Mindshield;
using Content.Shared._Starlight.Mindshield.Components;
using Content.Shared.Alert;

namespace Content.Client._Starlight.Mindshield;

/// <summary>
/// Client-side system for handling mindshield degradation UI updates.
/// </summary>
public sealed class MindshieldDegradationSystem : SharedMindshieldDegradationSystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<MindshieldDegradationComponent>();

        while (enumerator.MoveNext(out var uid, out var degradation))
        {
            // Skip if already complete
            if (degradation.DegradationComplete)
                continue;

            // Update alert severity based on degradation progress
            UpdateAlertSeverity(uid, degradation);
        }
    }

    /// <summary>
    /// Updates the alert severity based on degradation progress
    /// </summary>
    private void UpdateAlertSeverity(EntityUid uid, MindshieldDegradationComponent degradation)
    {
        var progress = GetDegradationProgress(uid, degradation);
        
        short severity = 0;
        if (progress >= 0.2f) // 2+ minutes elapsed
            severity = 1;
        if (progress >= 0.4f) // 4+ minutes elapsed
            severity = 2;
        if (progress >= 0.6f) // 6+ minutes elapsed
            severity = 3;
        if (progress >= 0.8f) // 8+ minutes elapsed  
            severity = 4;

        // Calculate cooldown times for the circular progress indicator
        var startTime = degradation.StartTime;
        var endTime = startTime + degradation.DegradationTime;
        var cooldown = (startTime, endTime);

        // Show the alert with cooldown information
        _alertsSystem.ShowAlert(uid, "MindshieldDegrading", severity, cooldown);
    }
}
