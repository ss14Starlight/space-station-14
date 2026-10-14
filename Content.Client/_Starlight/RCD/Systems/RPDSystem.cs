using Content.Client.Items;
using Content.Client.Message;
using Content.Client._Starlight.Computers.RemoteControl;
using Content.Client._Starlight.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.RCD.Systems;

public sealed partial class RPDSystem : EntitySystem
{
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlacementManager _placementManager = default!;
    [Dependency] private RemoteControlInterface _remoteControl = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<RCDComponent>(OnItemStatus);
    }

    private Control OnItemStatus(Entity<RCDComponent> entity)
        => new RPDModeStatusControl(entity);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_remoteControl.RemoteMousePosition is null
            || !_placementManager.IsActive
            || _placementManager.CurrentMode is not AlignRPDAtmosPipeLayers mode)
            return;

        mode.AlignPlacementMode(_inputManager.MouseScreenPosition);
    }

    private sealed class RPDModeStatusControl : Control
    {
        private readonly RichTextLabel _label = new()
        {
            StyleClasses = { "ItemStatus" }
        };

        private readonly EntityUid _uid;
        private readonly bool _isRpd;
        private readonly RCDSystem _rcdSystem;

        public RPDModeStatusControl(Entity<RCDComponent> entity)
        {
            _uid = entity.Owner;
            _isRpd = entity.Comp.IsRpd || entity.Comp.IsRPLD;
            _rcdSystem = Get<RCDSystem>();
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            if (!_isRpd) return;

            base.FrameUpdate(args);

            var currentMode = _rcdSystem.GetCurrentRpdMode(_uid);

            var modeKey = $"rcd-rpd-mode-{currentMode.ToString().ToLowerInvariant()}";
            var modeName = Robust.Shared.Localization.Loc.GetString(modeKey);

            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("rcd-item-status-mode",
                ("mode", $"[color=cyan]{modeName}[/color]")));
        }
    }
}
