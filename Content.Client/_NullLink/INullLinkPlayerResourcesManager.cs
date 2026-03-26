using System.Diagnostics.CodeAnalysis;
using Content.Shared._NullLink;

namespace Content.Client._NullLink;

public interface INullLinkPlayerResourcesManager : ISharedNullLinkPlayerResourcesManager
{
    event Action PlayerResourcesChanged;

    bool TryGetResources([NotNullWhen(true)] out Dictionary<string, double>? value);

    bool TryGetResource(string id, [NotNullWhen(true)] out double? value);
}