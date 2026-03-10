using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;

namespace Content.Shared._NullLink;

public interface ISharedNullLinkPlayerResourcesManager
{
    void Initialize();

    #region Setters

    bool TrySetResource(EntityUid uid, string id, double value, bool skipNullLink = false);

    bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false);

    bool TryUpdateResource(EntityUid uid, string id, double value, bool skipNullLink = false);

    bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false);

    bool TrySetResources(EntityUid uid, Dictionary<string, double> value);

    bool TrySetResources(ICommonSession session, Dictionary<string, double> value);

    #endregion

    #region Getters

    bool TryGetResources(EntityUid uid, [NotNullWhen(true)] out Dictionary<string, double>? value);

    bool TryGetResources(ICommonSession session, [NotNullWhen(true)] out Dictionary<string, double>? value);

    bool TryGetResource(EntityUid uid, string id, [NotNullWhen(true)] out double? value);

    bool TryGetResource(ICommonSession session, string id, [NotNullWhen(true)] out double? value);

    #endregion
}