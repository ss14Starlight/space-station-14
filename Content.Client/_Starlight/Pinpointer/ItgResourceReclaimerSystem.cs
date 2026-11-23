using Content.Client.Items;
using Content.Client.Message;
using Content.Shared._Starlight.Pinpointer;
using Content.Shared._Starlight.Pinpointer.Components;
using Content.Shared.Pinpointer;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Pinpointer;

public sealed partial class ItgResourceReclaimerSystem : SharedItgResourceReclaimerSystem
{
    [Dependency] private SpriteSystem _sprite = null!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.ItemStatus<ItgResourceReclaimerComponent>(OnItemStatus);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ItgResourceReclaimerComponent, PinpointerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var pinpointer, out var sprite))
        {
            if (pinpointer.HasTarget)
                continue;

            _sprite.LayerSetRotation((uid, sprite), PinpointerLayers.Screen, Angle.Zero);
        }
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
