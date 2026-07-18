using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Movement.Components
{
    /// <summary>
    /// Has additional movement data such as footsteps and weightless grab range for an entity.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class MobMoverComponent : Component
    {
        private float _stepSoundDistance;
        [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
        public float GrabRange = 1.0f;

        [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
        public float PushStrength = 600f;

        [DataField, AutoNetworkedField]
        public float StepSoundMoveDistanceRunning = 2;

        [DataField, AutoNetworkedField]
        public float StepSoundMoveDistanceWalking = 1.5f;

        [DataField, AutoNetworkedField]
        public float FootstepVariation;

        [ViewVariables(VVAccess.ReadWrite)]
        public EntityCoordinates LastPosition { get; set; }

        /// <summary>
        ///     Used to keep track of how far we have moved before playing a step sound
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float StepSoundDistance
        {
            get => _stepSoundDistance;
            set
            {
                if (MathHelper.CloseToPercent(_stepSoundDistance, value)) return;
                _stepSoundDistance = value;
            }
        }
    }
}
