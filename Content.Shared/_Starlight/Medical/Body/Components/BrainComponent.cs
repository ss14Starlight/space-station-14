using Content.Shared._Starlight.Medical.Body.Systems;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Body.Components;

[RegisterComponent, Access(typeof(BrainSystem))]
public sealed partial class BrainComponent : Component;
