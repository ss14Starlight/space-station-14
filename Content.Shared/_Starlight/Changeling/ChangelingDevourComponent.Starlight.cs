using Robust.Shared.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Changeling.Components;

public sealed partial class ChangelingDevourComponent
{
    /// <summary>
    /// Enables Starlight's living-prey and animal feeding rules without changing the default devour behavior.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UseStarlightDevour;

    /// <summary>
    /// Active feeding phase, synchronized to prevent duplicate predicted attempts.
    /// </summary>
    [AutoNetworkedField]
    public ushort? DoAfterIndex;

    /// <summary>
    /// Fraction of the consumption phase whose damage has already been applied.
    /// </summary>
    public float DamageProgress;

    /// <summary>
    /// Animal prey links to release when feeding or identity components are removed.
    /// </summary>
    public readonly HashSet<EntityUid> DevouredAnimals = new();
}
