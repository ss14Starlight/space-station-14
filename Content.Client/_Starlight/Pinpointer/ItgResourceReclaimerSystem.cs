using Content.Client.Items;
using Content.Client.Message;
using Content.Shared._Starlight.Pinpointer.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Pinpointer;

public sealed class ItgResourceReclaimerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<ItgResourceReclaimerComponent>(OnItemStatus);
    }

    private static Control OnItemStatus(Entity<ItgResourceReclaimerComponent> entity) => new ModeStatusControl(entity.Comp);

    private sealed class ModeStatusControl : Control
    {
        private readonly RichTextLabel _label = new()
        {
            StyleClasses = { "ItemStatus" }
        };

        private readonly ItgResourceReclaimerComponent _reclaimer;
        private ItgResourceReclaimerMode? _currentMode;

        public ModeStatusControl(ItgResourceReclaimerComponent reclaimer)
        {
            _reclaimer = reclaimer;
            AddChild(_label);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            if (_currentMode == _reclaimer.Mode)
                return;

            _currentMode = _reclaimer.Mode;
            var modeName = Robust.Shared.Localization.Loc.GetString(
                $"itg-resource-reclaimer-mode-{_reclaimer.Mode.ToString().ToLowerInvariant()}");
            _label.SetMarkup(Robust.Shared.Localization.Loc.GetString("itg-resource-reclaimer-item-status-mode",
                ("mode", $"[color=cyan]{modeName}[/color]")));
        }
    }
}
