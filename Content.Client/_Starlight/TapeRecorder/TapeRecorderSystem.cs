using Content.Shared._Starlight.TapeRecorder;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.TapeRecorder;

/// <summary>
/// Required for client side prediction stuff
/// </summary>
public sealed class TapeRecorderSystem : SharedTapeRecorderSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _lastTickTime = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        //We need to know the exact time period that has passed since the last update to ensure the tape position is sync'd with the server
        //Since the client can skip frames when lagging, we cannot use frameTime
        var realTime = (float) (_timing.CurTime - _lastTickTime).TotalSeconds;
        _lastTickTime = _timing.CurTime;

        base.Update(realTime);
    }
}
