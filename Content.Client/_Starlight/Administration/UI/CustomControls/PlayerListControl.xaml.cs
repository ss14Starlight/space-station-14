using Content.Client.Administration.Systems;
using Content.Shared.Administration;

// ReSharper disable CheckNamespace
namespace Content.Client.Administration.UI.CustomControls;

public sealed partial class PlayerListControl
{
    private IReadOnlyList<PlayerInfo> _currentPlayerList =>
        _entManager.TrySystem<AdminSystem>(out var system) ? system.PlayerList : [];
    private AdminSystem? _subscribedSystem;
    private readonly IEntitySystemManager _systemManager;

    /// <summary>
    /// Raised after the list is refreshed with new player data. Starlight.
    /// </summary>
    public event Action? OnListPopulated;

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _systemManager.SystemLoaded += OnSystemLoaded;
        Subscribe();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _systemManager.SystemLoaded -= OnSystemLoaded;
        Unsubscribe();
    }

    private void OnSystemLoaded(object? sender, SystemChangedArgs args)
    {
        if (args.System is AdminSystem)
            Subscribe();
    }

    /// <summary>
    /// Listens to the current AdminSystem, dropping a stale one from before a reconnect, and refreshes the list from it.
    /// </summary>
    private void Subscribe()
    {
        if (!_entManager.TrySystem<AdminSystem>(out var system))
            return;

        if (_subscribedSystem != system)
        {
            Unsubscribe();
            system.PlayerListChanged += PopulateList;
            _subscribedSystem = system;
        }

        PopulateList(system.PlayerList);
    }

    private void Unsubscribe()
    {
        _subscribedSystem?.PlayerListChanged -= PopulateList;
        _subscribedSystem = null;
    }
}
