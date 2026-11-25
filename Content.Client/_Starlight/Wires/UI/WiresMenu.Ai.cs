using System.Diagnostics;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using FrameEventArgs = Robust.Shared.Timing.FrameEventArgs;

// ReSharper disable CheckNamespace
namespace Content.Client.Wires.UI;

public sealed partial class WiresMenu
{
    private static readonly TimeSpan _stationAiWireCutHoldDuration = TimeSpan.FromSeconds(1);

    private bool _stationAiInteraction;

    public event Action<int, bool>? StationAiWireCutHoldChanged;

    public void SetStationAiInteraction(bool enabled) => _stationAiInteraction = enabled;

    private sealed partial class WireControl
    {
        private readonly bool _stationAiInteraction;
        private Stopwatch? _stationAiWirePressTimer;
        private bool _stationAiWirePressIsWire;
        private ProgressBar? _stationAiWireHoldProgress;
        private StyleBoxFlat? _stationAiWireHoldProgressStyle;

        public event Action<bool>? StationAiWireCutHoldChanged;

        private void InitializeStationAiWireHoldProgress(LayoutContainer layout)
        {
            var progressStyle = new StyleBoxFlat();
            _stationAiWireHoldProgressStyle = progressStyle;
            _stationAiWireHoldProgress = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                MinSize = new Vector2(20, 6),
                MaxHeight = 6,
                Visible = false,
                MouseFilter = MouseFilterMode.Ignore,
                ForegroundStyleBoxOverride = progressStyle
            };

            layout.AddChild(_stationAiWireHoldProgress);
            LayoutContainer.SetPosition(_stationAiWireHoldProgress, new Vector2(0, 82));
            SetStationAiWireHoldProgress(0f);
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);

            if (!_stationAiWirePressIsWire || _stationAiWirePressTimer == null)
                return;

            var position = args.RelativePosition;
            if (position.X >= 0 && position.X < Size.X && position.Y is > 20 and < 60)
                return;

            CancelStationAiWireCutHold();
        }

        private void BeginStationAiWireClick(GUIBoundKeyEventArgs args)
        {
            UserInterfaceManager.ControlFocused = this;
            _stationAiWirePressIsWire = args.RelativePosition.Y is > 20 and < 60;
            _stationAiWirePressTimer?.Stop();
            _stationAiWirePressTimer = Stopwatch.StartNew();

            if (_stationAiWirePressIsWire)
                StationAiWireCutHoldChanged?.Invoke(true);

            if (_stationAiWireHoldProgress == null) return;

            _stationAiWireHoldProgress.Visible = _stationAiWirePressIsWire;
            SetStationAiWireHoldProgress(0f);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (!_stationAiInteraction
                || _stationAiWirePressTimer == null
                || !_stationAiWirePressIsWire
                || _stationAiWireHoldProgress == null)
                return;

            SetStationAiWireHoldProgress((float) (
                _stationAiWirePressTimer.Elapsed.TotalSeconds / _stationAiWireCutHoldDuration.TotalSeconds));
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);

            if (!_stationAiInteraction
                || args.Function != EngineKeyFunctions.UIClick
                || _stationAiWirePressTimer == null)
                return;

            var elapsed = _stationAiWirePressTimer.Elapsed;
            var wasWirePress = _stationAiWirePressIsWire;
            _stationAiWirePressTimer.Stop();
            _stationAiWirePressTimer = null;
            _stationAiWirePressIsWire = false;

            if (_stationAiWireHoldProgress != null)
            {
                _stationAiWireHoldProgress.Visible = false;
                SetStationAiWireHoldProgress(0f);
            }

            if (!wasWirePress)
                ContactsClicked?.Invoke();
            else if (elapsed >= _stationAiWireCutHoldDuration)
                WireClicked?.Invoke();
            else
                StationAiWireCutHoldChanged?.Invoke(false);
        }

        private void CancelStationAiWireCutHold()
        {
            _stationAiWirePressTimer?.Stop();
            _stationAiWirePressTimer = null;
            _stationAiWirePressIsWire = false;

            if (_stationAiWireHoldProgress != null)
            {
                _stationAiWireHoldProgress.Visible = false;
                SetStationAiWireHoldProgress(0f);
            }

            StationAiWireCutHoldChanged?.Invoke(false);
        }

        private void SetStationAiWireHoldProgress(float progress)
        {
            if (_stationAiWireHoldProgress == null || _stationAiWireHoldProgressStyle == null)
                return;

            progress = Math.Clamp(progress, 0f, 1f);
            _stationAiWireHoldProgress.Value = progress;
            _stationAiWireHoldProgressStyle.BackgroundColor = Color.FromHsv(
                new Vector4((1f - progress) / 3f, 1f, 1f, 1f));
        }
    }
}
