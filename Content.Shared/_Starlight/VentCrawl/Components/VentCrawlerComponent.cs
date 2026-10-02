using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.VentCrawl.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VentCrawlerComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public bool InTube = false;

    [DataField]
    public float Speed = 0.35f;

    [DataField]
    public SoundCollectionSpecifier CrawlSound { get; set; } = new("VentClaw", AudioParams.Default.WithVolume(5f));

    public float EnterDelay = 2.5f;

    // Starlight Start
    [DataField]
    public bool MayCarryItems = true;
    //Starlight End
}

[Serializable, NetSerializable]
public sealed partial class EnterVentDoAfterEvent : SimpleDoAfterEvent
{
}
