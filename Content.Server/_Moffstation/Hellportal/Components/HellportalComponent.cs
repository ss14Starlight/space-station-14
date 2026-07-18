using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Moffstation.Hellportal.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class HellportalComponent : Component
{
    /// <summary>
    /// List of entities that can spawn from this portal.
    /// TODO: create an AdvancedSpawnTable and BossSpawnTable for ramping progression
    /// </summary>
    [DataField(required: true)]
    public EntityTableSelector BasicSpawnTable;

    /// <summary>
    /// When the next spawn wave will trigger.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextSpawn = TimeSpan.Zero;

    /// <summary>
    /// Cooldown duration between hellportal spawn waves, in seconds.
    /// </summary>
    [DataField]
    public TimeSpan SpawnCooldown = TimeSpan.FromSeconds(30f);

    /// <summary>
    /// If the number of existing hellportal-spawned entities exceed this number,
    /// the hellportal will not spawn any further entities.
    /// </summary>
    [DataField]
    public int MaxSpawns = 100;

    /// <summary>
    /// Determines the sound to play on spawn trigger, if not null.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

}
