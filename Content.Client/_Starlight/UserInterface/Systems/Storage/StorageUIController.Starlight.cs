// ReSharper disable CheckNamespace
using Content.Client._Starlight.Replay;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Storage;

public sealed partial class StorageUIController
{
    /// <summary>
    /// Whether bag windows dock into the hands hotbar. The handless replay observer has no hotbar, so its windows float.
    /// </summary>
    public bool UsesDockedWindows() => StaticStorageUIEnabled && !EntityManager.System<ReplayObserverSystem>().IsViewing();

    // In replays, E on an item in a bag opens it like E in the world does, including nested bags.
    private bool TryReplayActivateItem(GUIBoundKeyEventArgs args, EntityUid item)
    {
        var replay = EntityManager.System<ReplayObserverSystem>();
        if (!replay.IsViewing() || !replay.TryOpenDefaultView(item))
            return false;

        args.Handle();
        return true;
    }
}
