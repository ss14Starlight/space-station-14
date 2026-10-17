// ReSharper disable CheckNamespace
using Content.Client._Starlight.Replay;
using Content.Client.UserInterface.Controls;
using Content.Shared.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.Inventory;

public sealed partial class StrippableBoundUserInterface
{
    // In replays, left-click or E opens a bag instead of stripping, and right-click opens the item's verbs.
    private bool TryReplaySlotPressed(GUIBoundKeyEventArgs ev, SlotControl slot)
    {
        var replay = EntMan.System<ReplayObserverSystem>();
        if (!replay.IsViewing())
            return false;

        if (ev.Function == EngineKeyFunctions.Use || ev.Function == ContentKeyFunctions.ActivateItemInWorld)
        {
            if (slot.Entity is { } item)
                replay.TryViewContents(item);

            return true;
        }

        if (ev.Function == EngineKeyFunctions.UseSecondary && slot.Entity is { } target)
        {
            replay.OpenVerbMenu(target);
            return true;
        }

        return false;
    }
}
