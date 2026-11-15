namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Difficulty scoring excludes every free objective.
/// </summary>
public readonly record struct ObjectiveDifficultyScore(float Selected, float Completed)
{
    public float Percentage => Selected > 0 ? 100 * Completed / Selected : 0;

    public ObjectiveDifficultyScore Add(float difficulty, bool completed)
        => !float.IsFinite(difficulty) || difficulty <= 0 ? this :
            new(Selected + difficulty, Completed + (completed ? difficulty : 0));
}
