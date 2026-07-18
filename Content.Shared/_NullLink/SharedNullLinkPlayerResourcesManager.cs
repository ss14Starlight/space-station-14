using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._NullLink;

/// <summary>
/// Authoritative per-session player resource store. Does not depend on PlayerRolesManager.
/// </summary>
public abstract partial class SharedNullLinkPlayerResourcesManager : ISharedNullLinkPlayerResourcesManager
{
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    protected ISawmill _sawmill = default!;

    private readonly object _lock = new();
    private readonly Dictionary<NetUserId, Dictionary<string, double>> _resources = new();

    public virtual void Initialize()
        => _sawmill = _logManager.GetSawmill("_null.resources");

    public bool RemoveResources(ICommonSession session, [NotNullWhen(true)] out Dictionary<string, double>? finalResources)
    {
        lock (_lock)
        {
            if (!_resources.Remove(session.UserId, out var stored))
            {
                finalResources = null;
                return false;
            }

            finalResources = stored;
            return true;
        }
    }

    #region Setters

    public bool TrySetResource(EntityUid uid, string id, double value, bool skipNullLink = false)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return false;
        return TrySetResource(session, id, value, skipNullLink);
    }

    public virtual bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        double oldValue;
        lock (_lock)
        {
            var bag = GetOrCreate(session.UserId);
            bag.TryGetValue(id, out oldValue);
            if (value == oldValue)
                return false;

            bag[id] = value;
        }

        OnResourceChanged(session, id, oldValue, value, skipNullLink);
        return true;
    }

    public bool TryUpdateResource(EntityUid uid, string id, double value, bool skipNullLink = false)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return false;
        return TryUpdateResource(session, id, value, skipNullLink);
    }

    public virtual bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
    {
        if (value == 0)
            return false;

        double oldValue;
        double newValue;
        lock (_lock)
        {
            var bag = GetOrCreate(session.UserId);
            bag.TryGetValue(id, out oldValue);
            newValue = oldValue + value;
            bag[id] = newValue;
        }

        OnResourceChanged(session, id, oldValue, newValue, skipNullLink);
        return true;
    }

    public bool TrySetResources(EntityUid uid, Dictionary<string, double> value)
    {
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return false;
        return TrySetResources(session, value);
    }

    public virtual bool TrySetResources(ICommonSession session, Dictionary<string, double> value)
    {
        Dictionary<string, double> copy;
        lock (_lock)
        {
            copy = new Dictionary<string, double>(value);
            _resources[session.UserId] = copy;
        }

        OnResourcesReplaced(session, copy);
        return true;
    }

    #endregion

    #region Getters

    public bool TryGetResources(EntityUid uid, [NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        value = null;
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return false;
        return TryGetResources(session, out value);
    }

    public virtual bool TryGetResources(ICommonSession session, [NotNullWhen(true)] out Dictionary<string, double>? value)
    {
        lock (_lock)
        {
            if (!_resources.TryGetValue(session.UserId, out var stored))
            {
                value = null;
                return false;
            }

            // Return a copy so callers cannot mutate manager state.
            value = new Dictionary<string, double>(stored);
            return true;
        }
    }

    public bool TryGetResource(EntityUid uid, string id, [NotNullWhen(true)] out double? value)
    {
        value = null;
        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return false;
        return TryGetResource(session, id, out value);
    }

    public virtual bool TryGetResource(ICommonSession session, string id, [NotNullWhen(true)] out double? value)
    {
        lock (_lock)
        {
            value = null;
            if (!_resources.TryGetValue(session.UserId, out var stored)
                || !stored.TryGetValue(id, out var storedValue))
                return false;

            value = storedValue;
            return true;
        }
    }

    /// <summary>
    /// Replaces local store contents without raising change callbacks. Used by the client network handler.
    /// </summary>
    protected void ReplaceLocalResources(NetUserId userId, Dictionary<string, double> resources)
    {
        lock (_lock)
        {
            _resources[userId] = new Dictionary<string, double>(resources);
        }
    }

    #endregion

    protected virtual void OnResourceChanged(
        ICommonSession session,
        string id,
        double oldValue,
        double newValue,
        bool skipNullLink)
    {
    }

    protected virtual void OnResourcesReplaced(ICommonSession session, Dictionary<string, double> resources)
    {
    }

    private Dictionary<string, double> GetOrCreate(NetUserId userId)
    {
        if (!_resources.TryGetValue(userId, out var bag))
        {
            bag = new Dictionary<string, double>();
            _resources[userId] = bag;
        }

        return bag;
    }
}
