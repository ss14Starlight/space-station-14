using Content.Shared.Containers.ItemSlots;
using Content.Shared.MassMedia.Systems;

// ReSharper disable once CheckNamespace once
namespace Content.Client.MassMedia.Ui;

public sealed partial class NewsWriterBoundUserInterface
{
    private void OnPhotoEjectButtonPressed()
        => SendMessage(new ItemSlotButtonPressedEvent(SharedNewsSystem.PhotoSlotId));
}
