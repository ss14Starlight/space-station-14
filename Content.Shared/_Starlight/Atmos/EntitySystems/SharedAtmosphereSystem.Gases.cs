namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    public abstract bool IsMixtureModerator(GasMixture mixture, float epsilon = 0.001f);
}
