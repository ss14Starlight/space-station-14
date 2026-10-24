using Content.Client.UserInterface.Controls;
using Content.Shared.Hands.Components;
using Robust.Client.UserInterface.Controls; // Starlight
using System.Numerics; // Starlight

namespace Content.Client.UserInterface.Systems.Hands.Controls;

public sealed class HandButton : SlotControl
{
    public HandLocation HandLocation { get; }
    #region Starlight
    public TextureRect ItemIcon { get; }
    #endregion Starlight

    public HandButton(string handName, HandLocation handLocation)
    {
        HandLocation = handLocation;
        Name = "hand_" + handName;
        SlotName = handName;
        SetBackground(handLocation);
        // Starlight start
        AddChild(ItemIcon = new TextureRect
        {
            Visible = false,
            TextureScale = new Vector2(2, 2),
            Stretch = TextureRect.StretchMode.KeepCentered,
            MouseFilter = MouseFilterMode.Ignore,
        });
        ItemIcon.SetPositionInParent(3);
        // Starlight end
    }

    private void SetBackground(HandLocation handLoc)
    {
        ButtonTexturePath = handLoc switch
        {
            HandLocation.Left => "Slots/hand_l",
            HandLocation.Middle => "Slots/hand_m",
            HandLocation.Right => "Slots/hand_r",
            HandLocation.Functional => "Slots/hand_m", // Starlight
            _ => ButtonTexturePath
        };
    }
}
