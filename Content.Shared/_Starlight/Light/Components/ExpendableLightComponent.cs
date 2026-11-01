using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Light.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExpendableLightComponent : Component
{
    /// <summary>
    ///     Status of light, whether or not it is emitting light.
    /// </summary>
    [ViewVariables]
    public bool Activated => CurrentState is ExpendableLightState.Lit or ExpendableLightState.Fading;

    /// <summary>
    /// Absolute game time at which the current state began. Networked so any client (including one that just
    /// entered PVS) can place itself in the state via <c>CurTime - StateStartTime</c> — the state end is
    /// derived (<see cref="StateStartTime"/> + <see cref="StateDuration"/>) and the fade animation is seeked
    /// to that elapsed offset instead of replaying from zero.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan StateStartTime;

    /// <summary>
    /// How long the current state lasts. For an unlit light this is the burn time the next ignition will use
    /// (seeded from <see cref="GlowDuration"/>, adjusted by botany potency / refuelling); once Lit or Fading
    /// it is that state's duration. The state ends at <see cref="StateStartTime"/> + this.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan StateDuration;

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public ExpendableLightState CurrentState;

    [DataField]
    public string TurnOnBehaviourID = string.Empty;

    [DataField]
    public string FadeOutBehaviourID = string.Empty;

    [DataField]
    public TimeSpan GlowDuration = TimeSpan.FromSeconds(60 * 15f);

    [DataField]
    public TimeSpan FadeOutDuration = TimeSpan.FromSeconds(60 * 5f);

    [DataField]
    public ProtoId<StackPrototype>? RefuelMaterialID;

    [DataField]
    public TimeSpan RefuelMaterialTime = TimeSpan.FromSeconds(15f);

    [DataField]
    public TimeSpan RefuelMaximumDuration = TimeSpan.FromSeconds(60 * 15f * 2);

    [DataField]
    public int PlantBurnTimeModifier; // Starlight Edit

    [DataField]
    public SoundSpecifier? LitSound;

    [DataField]
    public SoundSpecifier? LoopedSound;

    [DataField]
    public SoundSpecifier? DieSound;

    #region Audiovisual

    /// <summary>
    /// The icon state used by expendable lights when the they have been completely expended.
    /// </summary>
    [DataField("iconStateSpent")]
    public string? IconStateSpent;

    /// <summary>
    /// The icon state used by expendable lights while they are lit.
    /// </summary>
    [DataField("iconStateLit")]
    public string? IconStateLit;

    /// <summary>
    /// The sprite layer shader used while the expendable light is lit.
    /// </summary>
    [DataField("spriteShaderLit")]
    public string? SpriteShaderLit = null;

    /// <summary>
    /// The sprite layer shader used after the expendable light has burnt out.
    /// </summary>
    [DataField("spriteShaderSpent")]
    public string? SpriteShaderSpent = null;

    /// <summary>
    /// The sprite layer shader used after the expendable light has burnt out.
    /// </summary>
    [DataField("glowColorLit")]
    public Color? GlowColorLit = null;

    /// <summary>
    /// If true, the <see cref="ExpendableLightVisualLayers.Glow"/> layer's alpha tracks the point light's
    /// (animated) energy while lit or fading, so an unshaded glow sprite fades in and out with the light
    /// instead of snapping on and off.
    /// </summary>
    [DataField]
    public bool FadeGlowWithEnergy;

    /// <summary>
    /// The point light energy that maps to a fully opaque glow layer when <see cref="FadeGlowWithEnergy"/>
    /// is set. Usually the light's peak energy (the turn-on fade's end value).
    /// </summary>
    [DataField]
    public float GlowReferenceEnergy = 1f;

    /// <summary>
    /// The sound that plays when the expendable light is lit.
    /// </summary>
    public EntityUid? PlayingStream;

    #endregion
}

[Serializable, NetSerializable]
public enum ExpendableLightVisuals
{
    State,
    Behavior
}

[Serializable, NetSerializable]
public enum ExpendableLightState
{
    BrandNew,
    Lit,
    Fading,
    Dead
}

public enum ExpendableLightVisualLayers : byte
{
    Base = 0,
    Glow = 1,
    Overlay = 2,
}

