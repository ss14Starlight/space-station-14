using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Changeling;

[RegisterComponent, NetworkedComponent]
public sealed partial class ChangelingActionComponent : Component
{
    [DataField] public bool RequireBiomass = true;

    [DataField] public float ChemicalCost = 0;

    [DataField] public float? EvolvedChemicalCost;

    [DataField] public float BiomassCost = 0;

    [DataField] public bool UseInLesserForm = false;
}

#region Events - Basic

public sealed partial class OpenEvolutionMenuEvent : InstantActionEvent { }
public sealed partial class AbsorbDNAEvent : EntityTargetActionEvent { }
public sealed partial class StingExtractDNAEvent : EntityTargetActionEvent { }
public sealed partial class ChangelingTransformCycleEvent : InstantActionEvent { }
public sealed partial class ChangelingTransformEvent : InstantActionEvent { }
public sealed partial class EnterStasisEvent : InstantActionEvent { }
public sealed partial class ExitStasisEvent : InstantActionEvent { }
public sealed partial class ToggleChangelingBreathingEvent : InstantActionEvent { }

#endregion

#region Events - Combat

public sealed partial class CreateBoneShardEvent : InstantActionEvent { }
public sealed partial class ToggleChitinousArmorEvent : InstantActionEvent { }
public sealed partial class ShriekDissonantEvent : InstantActionEvent { }
public sealed partial class ShriekResonantEvent : InstantActionEvent { }
public sealed partial class ToggleStrainedMusclesEvent : InstantActionEvent { }

#endregion

#region Events - Sting

[DataDefinition, Serializable, NetSerializable]
public sealed partial class LearnChemicalStingEvent : EntityEventArgs { }
public sealed partial class OpenChangelingChemistryEvent : InstantActionEvent { }
public sealed partial class ChemicalStingEvent : EntityTargetActionEvent { }
public sealed partial class StingFakeArmbladeEvent : EntityTargetActionEvent { }
public sealed partial class StingInfectEvent : EntityTargetActionEvent { }

/// <summary>
/// Starts assimilation of an organic target into the performer's existing hive.
/// </summary>

public sealed partial class StingBlindEvent : EntityTargetActionEvent { }
public sealed partial class StingChemEvent : EntityTargetActionEvent
{
    /// <summary>
    /// Reagents and amounts delivered by a legacy reagent sting.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, FixedPoint2> Chems = new();
}
public sealed partial class StingTransformEvent : EntityTargetActionEvent { }

#endregion

#region Events - Utility

public sealed partial class ActionAnatomicPanaceaEvent : InstantActionEvent { }
public sealed partial class ActionAugmentedEyesightEvent : InstantActionEvent { }
public sealed partial class ActionNightEyesightEvent : InstantActionEvent { }
public sealed partial class ActionThermalEyesightEvent : InstantActionEvent { } // Starlight
public sealed partial class ActionBiodegradeEvent : InstantActionEvent { }
public sealed partial class ActionChameleonSkinEvent : InstantActionEvent { }
public sealed partial class ActionEphedrineOverdoseEvent : InstantActionEvent { }
public sealed partial class ActionDesoxyephedrineOverdoseEvent : InstantActionEvent { } // Starlight
public sealed partial class ActionAmalgamOverdoseEvent : InstantActionEvent { } // Starlight
public sealed partial class ActionFleshmendEvent : InstantActionEvent { }
public sealed partial class ActionLastResortEvent : InstantActionEvent { }
public sealed partial class ActionLesserFormEvent : InstantActionEvent { }
public sealed partial class ActionSpacesuitEvent : InstantActionEvent { }
public sealed partial class ActionNeocyteDisguiseEvent : InstantActionEvent { } // Starlight
public sealed partial class ActionHivemindAccessEvent : InstantActionEvent { }
public sealed partial class ActionContortBodyEvent : InstantActionEvent { }
public sealed partial class ChangelingEmagEvent : EntityTargetActionEvent { }
public sealed partial class ToggleChangelingRegenerationEvent : InstantActionEvent { }
public sealed partial class ToggleChangelingPainSuppressionEvent : InstantActionEvent { }
public sealed partial class ToggleChangelingShockHandEvent : InstantActionEvent { }

#endregion
