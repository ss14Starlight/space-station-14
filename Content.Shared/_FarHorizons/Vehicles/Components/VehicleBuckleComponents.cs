using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using DrawDepthTag = Robust.Shared.GameObjects.DrawDepth;
using System.Numerics;

namespace Content.Shared._FarHorizons.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleBuckleComponent : Component
{
    /// <summary>
    /// How long it will take to unbuckle a driver
    /// </summary>
    [DataField("unbuckleTime")]
    public TimeSpan duration = TimeSpan.FromSeconds(3f);

    /// <summary>
    /// Should stuns dismount the driver? 
    /// </summary>
    [DataField("dismountOnStun")]
    public bool stundismount = true;

    /// <summary>
    /// Should knockdowns dismount the driver?
    /// </summary>
    [DataField("dismountOnKnockdown")]
    public bool knockdowndismount = true;

    /// <summary>
    /// Should armor slow down vehicle
    /// </summary>
    [DataField("armorAffectsVehicle")]
    public bool armoraffectsvehicle = false;
    
    /// <summary>
    /// Should crashes eject driver
    /// </summary>
    [DataField("ejectOnCrash")]
    public bool EjectOnCrash = false;

    /// <summary>
    /// What the buckle offset is used for north
    /// </summary>
    [DataField("northOffset")]
    public Vector2 NorthOffset = Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for south
    /// </summary>
    [DataField("southOffset")]
    public Vector2 SouthOffset = Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for west
    /// </summary>
    [DataField("westOffset")]
    public Vector2 WestOffset= Vector2.Zero;

    /// <summary>
    /// What the buckle offset is used for east
    /// </summary>
    [DataField("eastOffset")]
    public Vector2 EastOffset = Vector2.Zero;

    /// <summary>
    /// What drawdepth is used for north
    /// </summary>
    [DataField("northDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int northDrawDepth = DrawDepthTag.Default + 7;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("southDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int southDrawDepth = DrawDepthTag.Default + 7;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("eastDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int eastDrawDepth = DrawDepthTag.Default + 7;

    /// <summary>
    /// What drawdepth is used for south
    /// </summary>
    [DataField("westDrawDepth", customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int westDrawDepth = DrawDepthTag.Default + 7;
}