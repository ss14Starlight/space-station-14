using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System.Numerics;

namespace Content.Shared._RMC14.Medical.IV;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class IVDripComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    [DataField, AutoNetworkedField]
    public string Slot = "pack";

    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(5);

    [DataField, AutoNetworkedField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(3);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan TransferAt;

    [DataField, AutoNetworkedField]
    public string AttachedState = "hooked";

    [DataField, AutoNetworkedField]
    public string UnattachedState = "unhooked";

    [DataField, AutoNetworkedField]
    public string NoBagState = "empty";

    /// <summary>
    ///     Percentages are from 0 to 100
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<(int Percentage, string State)> ReagentStates = new();

    [DataField, AutoNetworkedField]
    public Color FillColor;

    /// <summary>
    ///     From 0 to 100
    /// </summary>
    [DataField, AutoNetworkedField]
    public int FillPercentage;

    [DataField, AutoNetworkedField]
    public int Range = 2;

    [DataField]
    public DamageSpecifier? RipDamage;

    [DataField, AutoNetworkedField]
    public bool Injecting = true;

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> RipEmote = "Scream";

    /// <summary>
    /// Offset from the center of the entity where the IV line should start
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 LineOriginOffset = Vector2.Zero;
}

[Serializable, NetSerializable]
public enum IVDripVisualLayers
{
    Base,
    Reagent
}
