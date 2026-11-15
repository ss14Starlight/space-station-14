using System.Globalization;
using Content.Client.Replay;
using Content.Shared._Starlight.Replay;
using Robust.Client.Replays.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client._Starlight.Replay;

// Round time (from round start) in the replay control widget, plus a box to jump to one. The widget is engine UI, but its
// controls are public, so these are added into its layout rather than changing it.
public sealed partial class ReplayObserverSystem
{
    [Dependency] private ContentReplayPlaybackManager _contentReplay = default!;

    private const string RoundTimeFormat = @"mm\:ss";
    private const double MaxRoundSeconds = 100 * 24 * 60 * 60;

    // Far past any server's uptime. Replay times beyond it are corrupt, and adding them together could overflow.
    private static readonly TimeSpan _maxReplayTime = TimeSpan.FromDays(3650);

    private ReplayControlWidget? _roundTimeWidget;
    private Label? _roundTimeLabel;
    private LineEdit? _jumpEdit;
    private Button? _jumpButton;

    /// <summary>
    /// Server time the round started, or the recording start for replays without it, which matches for auto-recorded rounds.
    /// </summary>
    private TimeSpan _roundStart
    {
        get
        {
            if (_replayPlayback.Replay is not { } replay)
                return TimeSpan.Zero;

            // With corrupt replay times, round time is left equal to replay time so no offset gets added.
            if (replay.StartTime < TimeSpan.Zero
                || replay.StartTime > _maxReplayTime
                || (replay.Duration is { } length && (length < TimeSpan.Zero || length > _maxReplayTime)))
            {
                return replay.StartTime;
            }

            // A stored round start after the recording start can't be right, and would throw off the jump math.
            if (replay.YamlData.TryGet(ReplayRoundTime.MetaKeyRoundStart, out ValueDataNode? node)
                && long.TryParse(node.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
                && ticks >= 0
                && ticks <= replay.StartTime.Ticks)
            {
                return TimeSpan.FromTicks(ticks);
            }

            // Lobby recordings only have it in the final metadata, and it falls within the recording.
            if (_contentReplay.FinalRoundStart is { } final
                && final >= TimeSpan.Zero
                && replay.Duration is { } duration
                && final - replay.StartTime <= duration)
            {
                return final;
            }

            return replay.StartTime;
        }
    }

    /// <summary>
    /// Jumps to a round time like "1:23:45", "23:45" or "45".
    /// </summary>
    public bool TryJumpToRoundTime(string text, out string message)
    {
        if (_replayPlayback.Replay is not { } replay)
        {
            message = Loc.GetString("replay-observer-jump-not-replay");
            return false;
        }

        if (!TryParseRoundTime(text, out var roundTime))
        {
            message = Loc.GetString("replay-observer-jump-invalid", ("time", text));
            return false;
        }

        // The offset first, so a large start time is never added on its own.
        var target = roundTime + (_roundStart - replay.StartTime);
        if (target < TimeSpan.Zero)
            target = TimeSpan.Zero;
        else if (replay.Duration is { } duration && target > duration)
            target = duration;

        _replayPlayback.SetTime(target);
        message = Loc.GetString("replay-observer-jump-done", ("time", FormatRoundTime(target + (replay.StartTime - _roundStart))));
        return true;
    }

    private void UpdateRoundTimeWidget()
    {
        var widget = _uiManager.ActiveScreen?.GetWidget<ReplayControlWidget>();
        if (widget != _roundTimeWidget)
            AttachRoundTimeControls(widget);

        if (widget == null || _roundTimeLabel == null || _replayPlayback.Replay is not { } replay)
            return;

        // Same position the widget's own labels use, so they agree while scrubbing.
        var offset = replay.StartTime - _roundStart;
        var current = TimeSpan.FromSeconds(widget.TickSlider.Value) + offset;
        var end = replay.Duration is { } duration ? FormatRoundTime(duration + offset) : "N/A";
        _roundTimeLabel.Text = Loc.GetString("replay-observer-round-time", ("current", FormatRoundTime(current)), ("end", end));
    }

    private void AttachRoundTimeControls(ReplayControlWidget? widget)
    {
        DetachRoundTimeControls();
        _roundTimeWidget = widget;

        if (widget?.ServerTimeLabel.Parent is not { } labels || widget.StopButton.Parent is not { } buttons)
            return;

        _roundTimeLabel = new Label();
        labels.AddChild(_roundTimeLabel);
        _roundTimeLabel.SetPositionInParent(widget.ServerTimeLabel.GetPositionInParent() + 1);

        _jumpEdit = new LineEdit
        {
            PlaceHolder = Loc.GetString("replay-observer-jump-placeholder"),
            ToolTip = Loc.GetString("replay-observer-jump-tooltip"),
            MinWidth = 90,
            Margin = new Thickness(8, 0, 0, 0),
        };
        _jumpEdit.OnTextEntered += args => JumpFromBox(args.Text);

        _jumpButton = new Button
        {
            Text = Loc.GetString("replay-observer-jump-button"),
            ToolTip = Loc.GetString("replay-observer-jump-tooltip"),
        };
        _jumpButton.OnPressed += _ => JumpFromBox(_jumpEdit.Text);

        buttons.AddChild(_jumpEdit);
        buttons.AddChild(_jumpButton);
    }

    private void DetachRoundTimeControls()
    {
        _roundTimeLabel?.Orphan();
        _jumpEdit?.Orphan();
        _jumpButton?.Orphan();
        _roundTimeLabel = null;
        _jumpEdit = null;
        _jumpButton = null;
        _roundTimeWidget = null;
    }

    private void JumpFromBox(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        TryJumpToRoundTime(text.Trim(), out var message);
        if (_observer is { } observer && Exists(observer))
            _popup.PopupEntity(message, observer);

        _jumpEdit?.ReleaseKeyboardFocus();
    }

    // "h:mm:ss", "m:ss" or "s". TimeSpan.Parse would read "23:45" as hours and minutes.
    private static bool TryParseRoundTime(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var parts = text.Split(':', StringSplitOptions.None);
        if (parts.Length is < 1 or > 3)
            return false;

        var seconds = 0d;
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value)
                || value < 0)
            {
                return false;
            }

            seconds = (seconds * 60) + value;
        }

        // Far past any replay, and far enough from TimeSpan's limit that adding the round start can't overflow.
        if (seconds > MaxRoundSeconds)
            return false;

        time = TimeSpan.FromSeconds(seconds);
        return true;
    }

    // Total hours, so replays past a day don't wrap back to 00.
    private static string FormatRoundTime(TimeSpan time)
    {
        var duration = time.Duration();
        var text = $"{(long) duration.TotalHours:00}:{duration.ToString(RoundTimeFormat)}";
        return time < TimeSpan.Zero ? "-" + text : text;
    }
}
