using Robust.Shared.Prototypes;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Atmos.Prototypes;

namespace Content.Shared.Cargo;

public sealed partial class CargoOrderData
{
    /// <summary>
    /// Price when the order was added.
    /// </summary>
    [DataField]
    public int Price;

    /// <summary>
    /// The ID of the station this order belongs to.
    /// </summary>
    [DataField] public NetEntity StationId;

    /// <summary>
    /// The prototype ID of the ordered product.
    /// </summary>
    [DataField] public ProtoId<CargoProductPrototype> CargoProductId;

    /// <summary>
    /// The ordered gas, if it was a gas order.
    /// </summary>
    [DataField] public ProtoId<GasPrototype>? GasType;

    /// <summary>
    /// The amount of moles of gas were bought, if it was a gas order.
    /// </summary>
    [DataField] public float GasMoles;

    /// <summary>
    /// The temperature of the gas that was bought, if it was a gas order.
    /// </summary>
    [DataField] public float GasTemperature;

    /// <summary>
    /// Creates an upstream cargo-product order while retaining the Starlight data used by gas orders and tamper seals.
    /// </summary>
    public CargoOrderData(
        int orderId,
        CargoProductPrototype product,
        int amount,
        string requester,
        string reason,
        ProtoId<CargoAccountPrototype> account,
        NetEntity stationId)
        : this(orderId, product.ID, amount, requester, reason, account)
    {
        StationId = stationId;
        CargoProductId = product.ID;
        Price = product.Cost;
        GasType = product.GasType;
        GasMoles = product.GasMoles;
        GasTemperature = product.GasTemperature;
    }
}
