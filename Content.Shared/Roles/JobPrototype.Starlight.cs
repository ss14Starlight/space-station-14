using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>
    /// Default alignment when this job's mind has no active antagonist or special role.
    /// </summary>
    [DataField]
    public ProtoId<RoleTypePrototype> RoleType = "Neutral";

    /// <summary>
    /// Optional displayed faction when this job's mind has no active antagonist or special role.
    /// Defaults to the faction of the job's role type.
    /// </summary>
    [DataField]
    public LocId? RoleSubtype;

            #region Starlight
        /// <summary>
        /// Multiplier for this job's contribution to security antag scaling.
        /// Multiplied by the configured security weight.
        /// </summary>
        [DataField]
        public double AntagScalingSecurityWeight { get; private set; }

        /// <summary>
        /// Multiplier for this job's contribution to salvage antag scaling.
        /// Multiplied by the configured salvage weight.
        /// </summary>
        [DataField]
        public double AntagScalingSalvageWeight { get; private set; }

        /// <summary>
        /// Multiplier for this job's contribution to Central Command antag scaling.
        /// Multiplied by the configured Central Command weight.
        /// </summary>
        [DataField]
        public double AntagScalingCentcomWeight { get; private set; }
        #endregion
}
