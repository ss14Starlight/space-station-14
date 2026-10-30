using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;

// ReSharper disable once CheckNamespace
namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    /// <summary>
    /// The entity whose gas mixture is currently reacting, when the reaction was started with
    /// <see cref="React(GasMixture, IGasMixtureHolder?, EntityUid)"/>.
    /// </summary>
    /// <remarks>
    /// Hi this is really fucking doo doo ass, and it will prooooobably break if RT ever gets multithreading.
    /// Unfortunately engine has no way to go from component -> owner other than IComponent.Owner which is obsolete.
    /// I also do not feel like refactoring the entire damn system so this is the best you get. Either way I'm like,
    /// 90% sure this will work as-is for the time being.
    /// </remarks>
    public EntityUid? ReactingEntity { get; private set; }

    /// <summary>
    /// Reacts a mixture held by an entity, exposing that entity to reaction effects through <see cref="ReactingEntity"/>.
    /// </summary>
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, EntityUid holderEntity)
    {
        var previous = ReactingEntity;
        ReactingEntity = holderEntity;
        try
        {
            return React(mixture, holder);
        }
        finally
        {
            ReactingEntity = previous;
        }
    }

    public override bool IsMixtureModerator(GasMixture mixture)
    {
        Span<float> tmp = stackalloc float[Atmospherics.AdjustedNumberOfGases];
        NumericsHelpers.Multiply(mixture.Moles, GasModeratorMask, tmp);
        return NumericsHelpers.HorizontalAdd(tmp) > 5.0f;
    }
}
