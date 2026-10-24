using Content.Client.Actions;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Actions.Controls;
using Content.Client.UserInterface.Systems.Hands.Controls;
using Content.Shared.Hands.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.UserInterface;

/// <summary>Shared rendering for remote hand and action buttons; callers retain their own command handling.</summary>
public static class RemoteControlButtons
{
    public static HandButton CreateHand(string name, HandLocation location, EntityUid? item,
        EntProtoId? emptyRepresentative, bool selected, Action<GUIBoundKeyEventArgs, SlotControl> pressed)
    {
        var button = new HandButton(name, location) { Highlight = selected };
        if (item is { } entity)
            button.SetEntity(entity);
        else if (emptyRepresentative is { } representative)
            button.SetPrototype(representative, true);
        else
            button.SetEntity(null);
        button.Pressed += pressed;
        return button;
    }

    public static ActionButton CreateAction(IEntityManager entities, ActionsSystem actions, EntityUid action,
        bool selected, Action<GUIBoundKeyEventArgs, ActionButton> unpressed)
    {
        var button = new ActionButton(entities) { MinSize = new(64, 64) };
        button.UpdateData(action, actions);
        button.SetRemoteSelected(selected);
        button.ActionUnpressed += unpressed;
        return button;
    }
}
