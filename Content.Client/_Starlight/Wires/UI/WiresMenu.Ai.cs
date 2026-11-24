using System.Diagnostics;
using System.Numerics;
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

    public void SetStationAiInteraction(bool enabled) => _stationAiInteraction = enabled;

    private sealed partial class WireControl
    {
        private readonly bool _stationAiInteraction;
        private Stopwatch? _stationAiWirePressTimer;
        private bool _stationAiWirePressIsWire;
        private ProgressBar? _stationAiWireHoldProgress;

        private void InitializeStationAiWireHoldProgress(LayoutContainer layout)
        {
            _stationAiWireHoldProgress = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                MinSize = new Vector2(20, 6),
                MaxHeight = 6,
                Visible = false,
                MouseFilter = MouseFilterMode.Ignore
            };

            layout.AddChild(_stationAiWireHoldProgress);
            LayoutContainer.SetPosition(_stationAiWireHoldProgress, new Vector2(0, 82));
        }

        private void BeginStationAiWireClick(GUIBoundKeyEventArgs args)
        {
            UserInterfaceManager.ControlFocused = this;
            _stationAiWirePressIsWire = args.RelativePosition.Y is > 20 and < 60;
            _stationAiWirePressTimer?.Stop();
            _stationAiWirePressTimer = Stopwatch.StartNew();

            if (_stationAiWireHoldProgress == null) return;

            _stationAiWireHoldProgress.Visible = _stationAiWirePressIsWire;
            _stationAiWireHoldProgress.Value = 0;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (!_stationAiInteraction
                || _stationAiWirePressTimer == null
                || !_stationAiWirePressIsWire
                || _stationAiWireHoldProgress == null)
                return;

            _stationAiWireHoldProgress.Value = Math.Clamp(
                (float) (_stationAiWirePressTimer.Elapsed.TotalSeconds / _stationAiWireCutHoldDuration.TotalSeconds),
                0f,
                1f);
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
                _stationAiWireHoldProgress.Value = 0;
            }

            if (!wasWirePress)
                ContactsClicked?.Invoke();
            else if (elapsed >= _stationAiWireCutHoldDuration)
                WireClicked?.Invoke();
        }
    }
}
