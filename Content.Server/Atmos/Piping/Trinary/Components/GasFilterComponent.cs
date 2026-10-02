using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Server.Atmos.Piping.Trinary.Components
{
    [RegisterComponent]
    public sealed partial class GasFilterComponent : Component, ISerializationHooks // Starlight: serialisation for legacy mapped gas filters
    {
        [DataField]
        public bool Enabled = true;

        [DataField("inlet")]
        public string InletName = "inlet";

        [DataField("filter")]
        public string FilterName = "filter";

        [DataField("outlet")]
        public string OutletName = "outlet";

        [DataField]
        public float TransferRate = Atmospherics.MaxTransferRate;

        [DataField]
        public float MaxTransferRate = Atmospherics.MaxTransferRate;

        [DataField]
        public HashSet<Gas> FilteredGases = new(); // Starlight: multiple

        #region Starlight


        /// Legacy field definition that may be set on older maps. Value appended to FilteredGases and cleared.
        [DataField("filteredGas")] private Gas? _filteredGasObsolete;

        /// Handles FilteredGas => FilteredGases migration.
        void ISerializationHooks.AfterDeserialization()
        {
            if (_filteredGasObsolete == null) return;
            FilteredGases.Add(_filteredGasObsolete.Value);
            _filteredGasObsolete = null;
        }

        #endregion
}
}
