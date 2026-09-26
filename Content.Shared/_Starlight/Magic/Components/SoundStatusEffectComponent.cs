using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Content.Shared._Starlight.Magic.Systems;

namespace Content.Shared._Starlight.Magic.Components;

/// <summary>
/// Allows playing a sound when a StatusEffect begins and/or ends. Heavily based on <see cref="BaseEmitSoundComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SoundStatusEffectSystem))]
public sealed partial class SoundStatusEffectComponent : Component
{
    /// <summary>
    /// The <see cref="SoundSpecifier"/> to play.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? startSound;

    [DataField, AutoNetworkedField]
    public bool startPositional;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? endSound;

    [DataField, AutoNetworkedField]
    public bool endPositional;
}
