using System.Globalization;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Truncates only the displayed value; scoring and selection retain the original float.
/// </summary>
public static class ObjectiveDifficultyDisplay
{
    public static string Format(float value)
        => (Math.Truncate((double) value * 10) / 10).ToString("0.#", CultureInfo.CurrentCulture);
}
