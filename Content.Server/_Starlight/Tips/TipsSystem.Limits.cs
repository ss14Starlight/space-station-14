using Content.Shared._Starlight.CCVar;

// ReSharper disable once CheckNamespace
namespace Content.Server.Tips;

public sealed partial class TipsSystem
{
    private const float MaxSlideTime = 10f;
    private const float MinWaddleInterval = 0.1f;
    private const float MaxWaddleInterval = 5f;

    private void ClampTippy(ref string message, ref float speakTime, ref float slideTime, ref float waddleInterval)
    {
        var maxLength = _cfg.GetCVar(StarlightCCVars.TippyMaxLength);
        if (message.Length > maxLength)
            message = message[..maxLength];

        // NaN fails every comparison, so it is replaced before the clamps.
        speakTime = float.IsFinite(speakTime) ? Math.Clamp(speakTime, 0f, _cfg.GetCVar(StarlightCCVars.TippyMaxSpeakTime)) : 5f;
        slideTime = float.IsFinite(slideTime) ? Math.Clamp(slideTime, 0f, MaxSlideTime) : 3f;
        waddleInterval = float.IsFinite(waddleInterval) ? Math.Clamp(waddleInterval, MinWaddleInterval, MaxWaddleInterval) : 0.5f;
    }
}
