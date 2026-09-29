using Robust.Shared.Prototypes;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Atmos.Prototypes;

namespace Content.Shared.Cargo
{
    public sealed partial class CargoOrderData
    {
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
}
