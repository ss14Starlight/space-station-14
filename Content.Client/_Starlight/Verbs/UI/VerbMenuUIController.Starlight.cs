// ReSharper disable CheckNamespace
using Content.Client._Starlight.Verbs.UI;
using Content.Client.ContextMenu.UI;
using Content.Shared.Input;
using Content.Shared.Verbs;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Verbs.UI;

public sealed partial class VerbMenuUIController
{
    private static readonly int _interactionPriority = new InteractionVerb().TypePriority;

    private bool _closeOnMouseOut;
    private TimeSpan _mouseOutTime;

    /// <summary>
    /// Lists entries from <see cref="GetVerbMenuEntriesEvent"/> directly below the interaction verbs
    /// (and anything sorted above them).
    /// </summary>
    private void AddEntries(ContextMenuPopup popup)
    {
        if (_remoteControl.ControlledEntity != null
            || _playerManager.LocalEntity is not { } user
            || !EntityManager.TryGetEntity(CurrentTarget, out var target))
            return;

        var ev = new GetVerbMenuEntriesEvent(user);
        EntityManager.EventBus.RaiseLocalEvent(target.Value, ev);

        var index = 0;
        var position = 0;
        foreach (var child in popup.MenuBody.Children)
        {
            index++;
            if (child is VerbMenuElement { Verb.TypePriority: var priority } && priority >= _interactionPriority)
                position = index;
        }

        foreach (var entry in ev.Entries)
        {
            entry.OnKeyBindDown += args => OnEntryKeyBindDown(entry, args);
            _context.AddElement(popup, entry);
            entry.SetPositionInParent(position++);
        }
    }

    private void OnEntryKeyBindDown(VerbMenuEntryElement entry, GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.Use && args.Function != ContentKeyFunctions.ActivateItemInWorld)
            return;

        UIManager.ClickSound();
        entry.OnUse?.Invoke();
        args.Handle();

        if (entry.CloseMenu)
        {
            _context.Close();
            return;
        }

        _closeOnMouseOut = true;
        _mouseOutTime = TimeSpan.Zero;
    }

    // A menu kept open by an entry closes once the mouse has left every open context menu.
    public override void FrameUpdate(FrameEventArgs args)
    {
        if (!_closeOnMouseOut)
            return;

        if (_context.RootMenu is not { Visible: true })
        {
            _closeOnMouseOut = false;
            return;
        }

        if (IsMouseOverContextMenu())
        {
            _mouseOutTime = TimeSpan.Zero;
            return;
        }

        _mouseOutTime += TimeSpan.FromSeconds(args.DeltaSeconds);
        if (_mouseOutTime >= ContextMenuUIController.HoverDelay)
            _context.Close();
    }

    private bool IsMouseOverContextMenu()
    {
        for (var control = UIManager.CurrentlyHovered; control != null; control = control.Parent)
        {
            if (control is ContextMenuPopup)
                return true;
        }

        return false;
    }
}
