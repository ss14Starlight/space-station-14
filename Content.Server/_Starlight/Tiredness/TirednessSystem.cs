using Content.Shared._Starlight.Tiredness.Systems;

namespace Content.Server._Starlight.Tiredness;

public sealed partial class TirednessSystem : SharedTirednessSystem
{
    public new static readonly TimeSpan MaximumTiredness = SharedTirednessSystem.MaximumTiredness;
}
