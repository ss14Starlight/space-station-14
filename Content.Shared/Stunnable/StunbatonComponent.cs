using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Stunnable;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState, AutoGenerateComponentPause] // Starlight-edit
[Access(typeof(SharedStunbatonSystem))]
public sealed partial class StunbatonComponent : Component
{
    [DataField("energyPerUse"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float EnergyPerUse = 350;

    [DataField("sparksSound")]
    public SoundSpecifier SparksSound = new SoundCollectionSpecifier("sparks");

    #region Starlight

    [DataField]
    public bool ShieldBash = true;

    [DataField]
    public SoundSpecifier ShieldBashSound = new SoundPathSpecifier("/Audio/_Starlight/Weapons/shieldbash.ogg");

    [DataField]
    public string ShieldBashMessage = "stunbaton-shield-bash-message";

    [DataField]
    public TimeSpan BashDelay = TimeSpan.FromSeconds(1);

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LastBashTime = TimeSpan.FromSeconds(0);

    #endregion
}
