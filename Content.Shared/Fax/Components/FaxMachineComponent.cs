using Content.Shared.Cargo.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Paper;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

#region Starlight
using Content.Shared._Starlight.Fax;
#endregion

namespace Content.Shared.Fax.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FaxMachineComponent : Component
{
    /// <summary>
    /// Name with which the fax will be visible to others on the network
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("name")]
    public string FaxName { get; set; } = "Unknown";

    /// <summary>
    /// Sprite to use when inserting an object.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public string InsertingState = "inserting";

    /// <summary>
    /// Device address of fax in network to which data will be send
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("destinationAddress")]
    public string? DestinationFaxAddress { get; set; }

    /// <summary>
    /// Contains the item to be sent, assumes it's paper...
    /// </summary>
    [DataField(required: true)]
    public ItemSlot PaperSlot = new();

    /// <summary>
    /// Is fax machine should respond to pings in network
    /// This will make it visible to others on the network
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool ResponsePings { get; set; } = true;

    /// <summary>
    /// Should admins be notified on message receive
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool NotifyAdmins { get; set; } = false;

    /// <summary>
    /// Should that fax receive nuke codes send by admins. Probably should be captain fax only
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public bool ReceiveNukeCodes { get; set; } = false;

    /// <summary>
    /// Sound to play when fax printing new message
    /// </summary>
    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>
    /// Sound to play when fax successfully send message
    /// </summary>
    [DataField]
    public SoundSpecifier SendSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    /// <summary>
    /// Known faxes in network by address with fax names
    /// </summary>
    [ViewVariables]
    public Dictionary<string, KnownFax> KnownFaxes { get; } = new(); // Starlight: string => KnownFax

    /// <summary>
    /// Print queue of the incoming message
    /// </summary>
    [ViewVariables]
    [DataField]
    public Queue<FaxPrintout> PrintingQueue { get; private set; } = new();

    /// <summary>
    /// Message sending timeout
    /// </summary>
    [ViewVariables]
    [DataField]
    public float SendTimeoutRemaining;

    /// <summary>
    /// Message sending timeout
    /// </summary>
    [ViewVariables]
    [DataField]
    public float SendTimeout = 5f;

    /// <summary>
    /// Remaining time of inserting animation
    /// </summary>
    [DataField]
    public float InsertingTimeRemaining;

    /// <summary>
    /// How long the inserting animation will play
    /// </summary>
    [ViewVariables]
    public float InsertionTime = 1.88f; // 0.02 off for correct animation

    /// <summary>
    /// Remaining time of printing animation
    /// </summary>
    [DataField]
    public float PrintingTimeRemaining;

    /// <summary>
    /// How long the printing animation will play
    /// </summary>
    [ViewVariables]
    public float PrintingTime = 2.3f;

    /// <summary>
    ///     The prototype ID to use for faxed or copied entities if we can't get one from
    ///     the paper entity for whatever reason.
    /// </summary>
    [DataField]
    public EntProtoId PrintPaperId = "Paper";

    /// <summary>
    ///     The prototype ID to use for faxed or copied entities if we can't get one from
    ///     the paper entity for whatever reason of the Office type.
    /// </summary>
    [DataField]
    public EntProtoId PrintOfficePaperId = "PaperOffice";

    #region Starlight
    /// <summary>
    /// The current group the fax machine appears in. Affects the color and ordering in the fax machine UI.
    /// </summary>
    [DataField("group")]
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FaxGroupPrototype>? CurrentGroup { get; set; }

    /// <summary>
    /// An intrinsic group this fax machine belongs to, if any. If this is set, the fax machine can always be configured
    /// to be part of that group, even if the group itself is not normally accessible.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FaxGroupPrototype>? IntrinsicGroup { get; set; }

    /// <summary>
    /// Whether this fax machine is locked to its intrinsic group when configuring it. Emagging unlocks this.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IntrinsicLocked { get; set; }

    /// <summary>
    /// The order of this fax machine within its group. Lower values mean higher up in the list.
    /// A non-zero means other uncategorized fax machines can have it adjusted to show up before others.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int Order { get; set; } = 1_000;

    #endregion
}

[DataDefinition]
public sealed partial class FaxPrintout
{
    [DataField(required: true)]
    public string Name { get; private set; } = default!;

    [DataField]
    public string? Label { get; private set; }

    [DataField(required: true)]
    public string Content { get; private set; } = default!;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>), required: true)]
    public string PrototypeId { get; private set; } = default!;

    [DataField("stampState")]
    public string? StampState { get; private set; }

    [DataField("stampedBy")]
    public List<StampDisplayInfo> StampedBy { get; private set; } = new();

    [DataField]
    public bool Locked { get; private set; }

    #region Starlight
    // Cargo slips data
    [DataField]
    public string? Product{ get; private set; }

    [DataField]
    public string? Requester{ get; private set; }

    [DataField]
    public string? Reason{ get; private set; }

    [DataField]
    public int? OrderQuantity{ get; private set; }

    [DataField]
    public string? Account{ get; private set; }

    // Metadata

    /// <summary>
    /// Whether to retain existing metadata when printing the fax. Relevant when copying instead of sending.
    /// </summary>
    [DataField]
    public bool RetainMetadata { get; private set; }

    /// <summary>
    /// The name of the sending fax machine.
    /// </summary>
    [DataField]
    public string? MetaSender { get; private set; }

    /// <summary>
    /// The formatted timestamp of when this fax was sent.
    /// </summary>
    [DataField]
    public string? MetaSentAt { get; private set; }
    #endregion

    private FaxPrintout()
    {
    }

    public FaxPrintout(
        string content,
        string name,
        string? label = null,
        string? prototypeId = null,
        string? stampState = null,
        List<StampDisplayInfo>? stampedBy = null,
        bool locked = false,
        //starlight-start
        string? product = null,
        string? requester = null,
        string? reason = null,
        int? orderQuantity = null,
        string? account = null,
        bool? retainMetadata = false,
        string? metaSender = null,
        string? metaSentAt = null
        //starlight-end
        )
    {
        Content = content;
        Name = name;
        Label = label;
        PrototypeId = prototypeId ?? "";
        StampState = stampState;
        StampedBy = stampedBy ?? new List<StampDisplayInfo>();
        Locked = locked;
        // Starlight-start
        Product = product;
        Requester = requester;
        Reason = reason;
        OrderQuantity = orderQuantity;
        Account = account;
        RetainMetadata = retainMetadata ?? false;
        MetaSender = metaSender;
        MetaSentAt = metaSentAt;
        // Starlight-end


    }


}

