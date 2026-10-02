using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry;

namespace Content.Server.Chemistry.Components
{
    /// <summary>
    /// An industrial grade chemical manipulator with pill and bottle production included.
    /// <seealso cref="ChemMasterSystem"/>
    /// </summary>
    public sealed partial class ChemMasterComponent : Component
    {
        /// <summary>
        ///     TRIESTE SPECIFIC
        ///     The transfer amount so the UI buttons make a click.
        /// </summary>
        [DataField]
        public ChemMasterReagentAmount TransferAmount = ChemMasterReagentAmount.U5;
    }
}
