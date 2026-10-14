// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    /// <summary>
    /// Can this gas mixture be considered to be unable to ignite due to the presence of moderator gases?
    /// </summary>
    public abstract bool IsMixtureModerator(GasMixture mixture);
}
