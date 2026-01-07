using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using DrawDepthTag = Robust.Shared.GameObjects.DrawDepth;
using System.Numerics;

namespace Content.Shared._FarHorizons.VehicleBuckle.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleBuckleComponent : Component
{
    /// <summary>
    /// How long it will take to unbuckle a driver
    /// </summary>
    [DataField("unbuckleTime"), AutoNetworkedField]
    public TimeSpan duration = TimeSpan.FromSeconds(3f);

    /// <summary>
    /// Should stuns dismount the driver? 
    /// </summary>
    [DataField("dismountOnStun"), AutoNetworkedField]
    public bool stundismount = true;

    /// <summary>
    /// Should knockdowns dismount the driver?
    /// </summary>
    [DataField("dismountOnKnockdown"), AutoNetworkedField]
    public bool knockdowndismount = true;

    /// <summary>
    /// Should knockdowns dismount the driver?
    /// </summary>
    [DataField("armorAffectsVehicle"), AutoNetworkedField]
    public bool armoraffectsvehicle = false;
    
    /// <summary>
    /// Should knockdowns dismount the driver?
    /// </summary>
    [DataField("ejectOnCrash"), AutoNetworkedField]
    public bool EjectOnCrash = false;

    /// <summary>
    /// What the buckle offset is used for north
    /// </summary>
    [DataField("northOffset"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 NorthOffset = Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for south
    /// </summary>
    [DataField("southOffset"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 SouthOffset = Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for west
    /// </summary>
    [DataField("westOffset"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 WestOffset= Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for east
    /// </summary>
    [DataField("eastOffset"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 EastOffset = Vector2.Zero;

    /// <summary>
    /// What drawdepth is used for north
    /// </summary>
    [DataField("northDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>)), AutoNetworkedField]
    public int northDrawDepth = DrawDepthTag.Default;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("southDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>)), AutoNetworkedField]
    public int southDrawDepth = DrawDepthTag.Default;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("eastDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>)), AutoNetworkedField]
    public int eastDrawDepth = DrawDepthTag.Default;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("westDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>)), AutoNetworkedField]
    public int westDrawDepth = DrawDepthTag.Default;
}