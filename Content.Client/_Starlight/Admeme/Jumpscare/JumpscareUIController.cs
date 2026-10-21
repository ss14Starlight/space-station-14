using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Admeme.Jumpscare;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Admeme.Jumpscare;

/// <summary>
/// Does the jumpscare.
/// </summary>
public sealed partial class JumpscareUIController : UIController
{
    [Dependency] private IGameTiming _timing = null!;
    [UISystemDependency] private readonly SpriteSystem _sprite = null!;

    private readonly List<ActiveJumpscare> _active = [];

    public void Play(JumpscareData data) =>
        _active.Add(new ActiveJumpscare(data, _timing.RealTime + data.StartDelay));

    public void PlayNow(JumpscareData data) =>
        _active.Add(new ActiveJumpscare(data, _timing.RealTime));

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.RealTime;
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var scare = _active[i];
            if (now < scare.StartAt)
                continue;

            if (!scare.Started)
                Start(scare);

            var elapsed = now - scare.StartAt;
            var data = scare.Data;
            if (elapsed >= data.TotalTime || scare.Rect is { Parent: null })
            {
                scare.Rect?.Orphan();
                _active.RemoveAt(i);
                continue;
            }

            if (scare.Rect == null || data.Sprite == null)
                continue;

            scare.Rect.Texture = _sprite.GetFrame(data.Sprite, elapsed);

            var alpha = 1f;
            var fadeOutAt = data.FadeInTime + data.HoldTime;
            if (elapsed < data.FadeInTime)
                alpha = (float) (elapsed / data.FadeInTime);
            else if (elapsed > fadeOutAt && data.FadeTime > TimeSpan.Zero)
                alpha = 1f - (float) ((elapsed - fadeOutAt) / data.FadeTime);
            scare.Rect.Modulate = Color.White.WithAlpha(Math.Clamp(alpha, 0f, 1f));
        }
    }

    private void Start(ActiveJumpscare scare)
    {
        scare.Started = true;

        if (scare.Data.Sprite == null)
            return;

        scare.Rect = new TextureRect
        {
            Texture = _sprite.GetFrame(scare.Data.Sprite, TimeSpan.Zero),
            // Avoid a one-frame flash before the fade in starts.
            Modulate = scare.Data.FadeInTime > TimeSpan.Zero ? Color.Transparent : Color.White,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        if (!scare.Data.RenderAboveUI
            && UIManager.ActiveScreen is { } screen
            && screen.GetWidget<MainViewport>() is { } viewport)
        {
            Control viewportRoot = viewport;
            while (viewportRoot.Parent is { } parent && parent != screen)
                viewportRoot = parent;

            screen.AddChild(scare.Rect);
            if (viewportRoot.Parent == screen)
                scare.Rect.SetPositionInParent(viewportRoot.GetPositionInParent() + 1);
        }
        else
        {
            var root = scare.Data.RenderAboveUI ? UIManager.PopupRoot : UIManager.StateRoot;
            root.AddChild(scare.Rect);
        }

        LayoutContainer.SetAnchorPreset(scare.Rect, LayoutContainer.LayoutPreset.Wide);
    }

    private sealed class ActiveJumpscare(JumpscareData data, TimeSpan startAt)
    {
        public readonly JumpscareData Data = data;
        public readonly TimeSpan StartAt = startAt;
        public bool Started;
        public TextureRect? Rect;
    }
}
