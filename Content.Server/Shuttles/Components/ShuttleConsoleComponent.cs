using System.Numerics;
using Content.Shared.Shuttles.Components;

namespace Content.Server.Shuttles.Components
{
    [RegisterComponent]
    [AutoGenerateComponentPause] // Starlight
    public sealed partial class ShuttleConsoleComponent : SharedShuttleConsoleComponent
    {
        [ViewVariables]
        public readonly List<EntityUid> SubscribedPilots = new();

        /// <summary>
        /// How much should the pilot's eye be zoomed by when piloting using this console?
        /// </summary>
        [DataField("zoom")]
        public Vector2 Zoom = new(1.5f, 1.5f);

        /// <summary>
        /// Should this console have access to restricted FTL destinations?
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite), DataField("whitelistSpecific")]
        public List<EntityUid> FTLWhitelist = new List<EntityUid>();

        #region Starlight
        /// <summary>
        /// When the last interface update was transmitted.
        /// </summary>
        [AutoPausedField]
        public TimeSpan LastInterfaceUpdateTime;
        #endregion
    }
}
