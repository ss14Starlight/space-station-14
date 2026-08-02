namespace Content.Server._Starlight.Medical.Virology;

internal enum PathogenContaminationMilestone
{
    AmbientLow,
    Emergent,
    AmbientHigh,
}

/// <summary>
/// Keeps contamination thresholds one-shot while allowing the meter itself to rise and fall.
/// </summary>
internal sealed class PathogenContaminationMilestones
{
    internal const float AmbientLowThreshold = 25f;
    internal const float EmergentThreshold = 50f;
    internal const float AmbientHighThreshold = 75f;

    private readonly HashSet<PathogenContaminationMilestone> _handled = new();

    public List<PathogenContaminationMilestone> GetPending(float contamination)
    {
        var pending = new List<PathogenContaminationMilestone>(3);

        AddIfPending(pending, PathogenContaminationMilestone.AmbientLow, contamination, AmbientLowThreshold);
        AddIfPending(pending, PathogenContaminationMilestone.Emergent, contamination, EmergentThreshold);
        AddIfPending(pending, PathogenContaminationMilestone.AmbientHigh, contamination, AmbientHighThreshold);

        return pending;
    }

    public void MarkHandled(PathogenContaminationMilestone milestone)
        => _handled.Add(milestone);

    public void Reset()
        => _handled.Clear();

    private void AddIfPending(
        List<PathogenContaminationMilestone> pending,
        PathogenContaminationMilestone milestone,
        float contamination,
        float threshold)
    {
        if (contamination >= threshold && !_handled.Contains(milestone))
            pending.Add(milestone);
    }
}
