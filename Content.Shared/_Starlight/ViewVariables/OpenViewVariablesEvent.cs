using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.ViewVariables;

[Serializable, NetSerializable]
public sealed class OpenViewVariablesEvent(string path) : EntityEventArgs
{
    public string Path { get; } = path;
}