// ReSharper disable CheckNamespace
namespace Content.Shared.Plumbing.EntitySystems;

[ByRefEvent]
public readonly record struct PlumbingOutputAutoStateEvent(bool Enabled);
