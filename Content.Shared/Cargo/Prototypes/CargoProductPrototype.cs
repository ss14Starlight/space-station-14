using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.Cargo.Prototypes
{
    [Prototype]
    public sealed partial class CargoProductPrototype : IPrototype, IInheritingPrototype
    {
        /// <inheritdoc />
        [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CargoProductPrototype>))]
        public string[]? Parents { get; private set; }

        /// <inheritdoc />
        [NeverPushInheritance]
        [AbstractDataField]
        public bool Abstract { get; private set; }

        [DataField("name")] private string _name = string.Empty;
        [DataField("nameLoc")] private string _nameLoc = string.Empty; // Starlight

        [DataField("description")] private string _description = string.Empty;

        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        /// <summary>
        ///     Product name.
        /// </summary>
        [ViewVariables]
        public string Name
        {
            get
            {
                if (_name.Trim().Length != 0)
                    return _name;

                if (!string.IsNullOrEmpty(_nameLoc)) // Starlight
                    return _name = Loc.GetString(_nameLoc); // Starlight

                if (!string.IsNullOrEmpty(Product) && // Starlight
                    IoCManager.Resolve<IPrototypeManager>().Resolve(Product, out EntityPrototype? prototype)) // Starlight
                {
                    _name = prototype.Name;
                }

                return _name;
            }
        }

        /// <summary>
        ///     Short description of the product.
        /// </summary>
        [ViewVariables]
        public string Description
        {
            get
            {
                if (_description.Trim().Length != 0)
                    return _description;

                if (!string.IsNullOrEmpty(Product) && // Starlight: Added not-null check
                    IoCManager.Resolve<IPrototypeManager>().Resolve(Product, out var prototype)) // Starlight
                {
                    _description = prototype.Description;
                }

                return _description;
            }
        }

        /// <summary>
        ///     Texture path used in the CargoConsole GUI.
        /// </summary>
        [DataField]
        public SpriteSpecifier Icon { get; private set; } = SpriteSpecifier.Invalid;

        /// <summary>
        ///     The entity prototype ID of the product.
        /// </summary>
        [DataField]
        public EntProtoId? Product { get; private set; } // Starlight: possibly empty string => possibly null EntProtoId

        /// <summary>
        ///     The point cost of the product.
        /// </summary>
        [DataField]
        public int Cost { get; private set; }

        /// <summary>
        ///     The prototype category of the product. (e.g. Engineering, Medical)
        /// </summary>
        [DataField]
        public string Category { get; private set; } = string.Empty;

        /// <summary>
        ///     The prototype group of the product. (e.g. Contraband)
        /// </summary>
        [DataField]
        public ProtoId<CargoMarketPrototype> Group { get; private set; } = "market";

        #region Starlight

        /// <summary>
        ///     The type of gas purchased, if any.
        /// </summary>
        [DataField]
        public ProtoId<GasPrototype>? GasType { get; private set; }

        /// <summary>
        ///     The amount of moles purchased.
        /// </summary>
        [DataField]
        public float GasMoles { get; private set; }

        /// <summary>
        ///     The temperature the moles will have when spawned.
        /// </summary>
        [DataField]
        public float GasTemperature { get; private set; } = Atmospherics.T20C;

        #endregion
    }
}
