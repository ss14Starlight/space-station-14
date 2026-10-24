using Robust.Shared.Utility;

namespace Content.Shared._Starlight.Railroading.Components.Tasks;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class RailroadTimerTaskComponent : Component
{
    [DataField]
    public LocId Message = "rail-timer-task";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(1);

    [DataField, AutoPausedField]
    public TimeSpan Started = TimeSpan.Zero;

    [DataField, AutoPausedField]
    public TimeSpan EndTime = TimeSpan.Zero;

    [DataField]
    public bool IsCompleted;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("Objects/Devices/goldwatch.rsi"), "goldwatch");
}

