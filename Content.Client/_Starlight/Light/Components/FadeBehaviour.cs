// ReSharper disable once CheckNamespace
namespace Content.Client.Light.Components;

public sealed partial class FadeBehaviour
{
    /*
     * Apply the value for the current playback position the instant the animation starts. Otherwise, the property keeps
     * its pre-animation value until the next frame.
     */
    public override void OnStart()
        => ApplyTime(StartOffset);

    public override (int KeyFrameIndex, float FramePlayingTime) AdvancePlayback(
        object context, int prevKeyFrameIndex, float prevPlayingTime, float frameTime)
    {
        var playingTime = prevPlayingTime + frameTime;
        ApplyTime(playingTime);
        return (-1, playingTime);
    }
}
