namespace Content.Shared._Starlight.GasMining.GasNode;

public abstract partial class SharedGasNodeSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
}
