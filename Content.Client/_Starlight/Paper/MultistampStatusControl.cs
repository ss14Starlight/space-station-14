using Content.Shared._Starlight.Paper;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Paper;

public sealed class MultistampStatusControl : Control
{
    private readonly MultistampComponent _parent;
    private readonly RichTextLabel _label;

    public MultistampStatusControl(MultistampComponent parent)
    {
        _parent = parent;
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        _label.SetMarkup(_parent.StatusShowStamp ? _parent.CurrentStampName : string.Empty);
        AddChild(_label);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_parent.UiUpdateNeeded)
        {
            _parent.UiUpdateNeeded = false;
            Update();
        }
    }

    public void Update()
        => _label.SetMarkup(_parent.StatusShowStamp ? _parent.CurrentStampName : string.Empty);
}
