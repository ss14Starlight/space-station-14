using Robust.Shared.Configuration;

// ReSharper disable CheckNamespace
namespace Content.Shared._Starlight.CCVar;

public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Selects the light-sleep sonar reveal method: 0 = sleeper waves, 1 = rotating line, 2 = cat-ear sweep.
    /// </summary>
    public static readonly CVarDef<int> LightSleepSonarRevealType =
        CVarDef.Create("light_sleep_sonar.reveal_type", 2, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Rotation speed of the sleeper-origin sonar sweep, in degrees per second.
    /// </summary>
    public static readonly CVarDef<float> LightSleepSonarSweepSpeedDegreesPerSecond =
        CVarDef.Create("starlight.light_sleep_sonar.sweep_speed_degrees_per_second", 20f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Screen-relative start angle for both cat-ear sweeps
    /// </summary>
    public static readonly CVarDef<float> LightSleepSonarCatEarStartAngleDegrees =
        CVarDef.Create("starlight.light_sleep_sonar.cat_ear_start_angle_degrees", 60f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Angular separation between the two lines; sweep travel is 180 degrees minus this value,
    /// with the right sweep inverted.
    /// </summary>
    public static readonly CVarDef<float> LightSleepSonarCatEarLineSeparationDegrees =
        CVarDef.Create("starlight.light_sleep_sonar.cat_ear_line_separation_degrees", 20f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
