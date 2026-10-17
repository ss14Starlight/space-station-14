// ReSharper disable CheckNamespace
namespace Content.Shared.Plumbing.EntitySystems;

[ByRefEvent]
public readonly record struct PlumbingOutputChangedEvent(bool Enabled);
