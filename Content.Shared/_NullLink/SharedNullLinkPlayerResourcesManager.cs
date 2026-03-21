using System.Diagnostics.CodeAnalysis;
using Content.Shared.Starlight;
using Robust.Shared.Player;

namespace Content.Shared._NullLink;

[Virtual]
public abstract class SharedNullLinkPlayerResourcesManager : ISharedNullLinkPlayerResourcesManager
{
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
    [Dependency] private readonly ISharedPlayersRoleManager _sharedPlayers = default!;

    protected ISawmill _sawmill = default!;


    public virtual void Initialize() 
        => _sawmill = _logManager.GetSawmill("_null.resources");

    #region Setters

    public bool TrySetResource(EntityUid uid, string id, double value, bool skipNullLink = false)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return false;
        return TrySetResource(session, id, value, skipNullLink);
    }

    public virtual bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (_sharedPlayers.GetPlayerData(session) is not { } data)
            return false;

        if (data.Resources.TryGetValue(id, out var oldValue) && value - oldValue == 0) // If we don't have any difference - we don't need to call null link.
            return false;

        data.Resources[id] = value;

        return true;
    }

    public bool TryUpdateResource(EntityUid uid, string id, double value, bool skipNullLink = false)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return false;
        return TryUpdateResource(session, id, value, skipNullLink);
    }

    public virtual bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (_sharedPlayers.GetPlayerData(session) is not { } data)
            return false;
        
        if (data.Resources.TryGetValue(id, out var current))
            data.Resources[id] = current + value;
        else
            data.Resources[id] = value;

        return true;
    }

    public bool TrySetResources(EntityUid uid, Dictionary<string, double> value)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return false;
        return TrySetResources(session, value);
    }

    public bool TrySetResources(ICommonSession session, Dictionary<string, double> value)
    {
        if (_sharedPlayers.GetPlayerData(session) is not { } data)
            return false;

        data.Resources = value;

        return true;
    }

    #endregion

    #region Getters

    public bool TryGetResources(EntityUid uid, [NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        value = null;
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return false;
        return TryGetResources(session, out value);
    }

    public bool TryGetResources(ICommonSession session, [NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        value = null;

        if (value == null)
            if (_sharedPlayers.GetPlayerData(session) is { } data)
                value = data.Resources;
            else
                return false;

        return true;
    }

    public bool TryGetResource(EntityUid uid, string id, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (!_playerManager.TryGetSessionByEntity(uid, out var session)) return false;
        return TryGetResource(session, id, out value);
    }

    public bool TryGetResource(ICommonSession session, string id, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (!TryGetResources(session, out var values) 
            || !values.TryGetValue(id, out var Value))
            return false;

        value = Value;

        return true;
    }

    #endregion
}
