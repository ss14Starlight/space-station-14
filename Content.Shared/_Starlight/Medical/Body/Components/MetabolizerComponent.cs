using Content.Shared._Starlight.Medical.Body.Prototypes;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Body.Components
{
    /// <summary>
    ///     Handles metabolizing various reagents with given effects.
    /// </summary>
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
    [Access(typeof(SharedMetabolizerSystem))]
    public sealed partial class MetabolizerComponent : Component
    {
        /// <summary>
        ///     The next time that reagents will be metabolized.
        ///     Not networked as the client does not use this.
        /// </summary>
        [DataField, AutoPausedField]
        public TimeSpan NextUpdate;

        /// <summary>
        ///     How often to metabolize reagents.
        /// </summary>
        /// <returns></returns>
        [DataField, AutoNetworkedField]
        public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Multiplier applied to <see cref="UpdateInterval"/> for adjusting based on metabolic rate multiplier.
        /// </summary>
        [DataField, AutoNetworkedField]
        public float UpdateIntervalMultiplier = 1f;

        /// <summary>
        /// Adjusted update interval based off of the multiplier value.
        /// </summary>
        [ViewVariables]
        public TimeSpan AdjustedUpdateInterval => UpdateInterval * UpdateIntervalMultiplier;

        /// <summary>
        ///     From which solution will this metabolizer attempt to metabolize chemicals
        /// </summary>
        [DataField("solution"), AutoNetworkedField]
        public string SolutionName = BloodstreamComponent.DefaultBloodSolutionName;

        /// <summary>
        ///     Does this component use a solution on it's parent entity (the body) or itself
        /// </summary>
        /// <remarks>
        ///     Most things will use the parent entity (bloodstream).
        /// </remarks>
        [DataField, AutoNetworkedField]
        public bool SolutionOnBody = true;

        /// <summary>
        ///     List of metabolizer types that this organ is. ex. Human, Slime, Felinid, w/e.
        /// </summary>
        [DataField, AutoNetworkedField]
        [Access(typeof(SharedMetabolizerSystem), Other = AccessPermissions.ReadExecute)] // FIXME Friends
        public HashSet<ProtoId<MetabolizerTypePrototype>>? MetabolizerTypes;

        /// <summary>
        ///     Should this metabolizer remove chemicals that have no metabolisms defined?
        ///     As a stop-gap, basically.
        /// </summary>
        [DataField, AutoNetworkedField]
        public bool RemoveEmpty;

        /// <summary>
        ///     How many reagents can this metabolizer process at once?
        ///     Used to nerf 'stacked poisons' where having 5+ different poisons in a syringe, even at low
        ///     quantity, would be muuuuch better than just one poison acting.
        /// </summary>
        [DataField("maxReagents"), AutoNetworkedField]
        public int MaxReagentsProcessable = 3;

        /// <summary>
        ///     A list of metabolism groups that this metabolizer will act on, in order of precedence.
        /// </summary>
        [DataField("groups"), AutoNetworkedField]
        public List<MetabolismGroupEntry>? MetabolismGroups;
    }

    /// <summary>
    ///     Contains data about how a metabolizer will metabolize a single group.
    ///     This allows metabolizers to remove certain groups much faster, or not at all.
    /// </summary>
    [DataDefinition]
    [Serializable, NetSerializable]
    public sealed partial class MetabolismGroupEntry
    {
        [DataField(required: true)]
        public ProtoId<MetabolismGroupPrototype> Id;

        [DataField("rateModifier")]
        public FixedPoint2 MetabolismRateModifier = 1.0;
    }
}
