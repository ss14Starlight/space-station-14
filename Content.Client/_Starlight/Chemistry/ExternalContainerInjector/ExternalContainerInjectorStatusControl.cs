using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Content.Shared._Starlight.Chemistry.ExternalContainerInjector;

namespace Content.Client._Starlight.Chemistry.ExternalContainerInjector;

public sealed class ExternalContainerInjectorStatusControl : Control
{
    private readonly Entity<ExternalContainerInjectorComponent> _parent;
    private readonly RichTextLabel _label;
    private readonly SharedSolutionContainerSystem _solutionContainers;
    private readonly ItemSlotsSystem _itemSlots;
    private FixedPoint2 _prevVolume = FixedPoint2.Zero;
    private FixedPoint2 _prevMaxVolume = FixedPoint2.Zero;
    private EntityUid? _prevVialEntity;

    public ExternalContainerInjectorStatusControl(Entity<ExternalContainerInjectorComponent> parent)
    {
        _parent = parent;
        _solutionContainers = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedSolutionContainerSystem>();
        _itemSlots = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ItemSlotsSystem>();

        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Get solution from the inserted vial
        if (!_itemSlots.TryGetSlot(_parent.Owner, _parent.Comp.VialSlotId, out var slot) || !slot.HasItem || slot.Item == null)
        {
            // Check if we need to update (when mode changes or when vial is removed)
            if (_prevVialEntity != null)
            {
                _prevVolume = FixedPoint2.Zero;
                _prevMaxVolume = FixedPoint2.Zero;
                _prevVialEntity = null;
            }

            _label.SetMarkup(Loc.GetString("brigmedic-hypospray-volume-label",
                ("currentVolume", FixedPoint2.Zero),
                ("totalVolume", FixedPoint2.Zero)));
            return;
        }

        // Check if the vial entity has changed
        bool vialChanged = _prevVialEntity != slot.Item.Value;
        if (vialChanged)
        {
            _prevVialEntity = slot.Item.Value;
            _prevVolume = FixedPoint2.Zero;
            _prevMaxVolume = FixedPoint2.Zero;
        }

        if (!_solutionContainers.TryGetSolution(slot.Item.Value, _parent.Comp.VialSolutionName, out _, out var solution))
        {
            // Check if we need to update (when mode changes or when solution is removed)
            if (_prevVolume != FixedPoint2.Zero)
            {
                _prevVolume = FixedPoint2.Zero;
                _prevMaxVolume = FixedPoint2.Zero;
            }

            _label.SetMarkup(Loc.GetString("brigmedic-hypospray-volume-label",
                ("currentVolume", FixedPoint2.Zero),
                ("totalVolume", FixedPoint2.Zero)));
            return;
        }

        // only updates the UI if any of the details are different than they previously were
        if (!vialChanged && _prevVolume == solution.Volume
            && _prevMaxVolume == solution.MaxVolume)
            return;

        _prevVolume = solution.Volume;
        _prevMaxVolume = solution.MaxVolume;

        _label.SetMarkup(Loc.GetString("brigmedic-hypospray-volume-label",
            ("currentVolume", solution.Volume),
            ("totalVolume", solution.MaxVolume)));
    }
} 