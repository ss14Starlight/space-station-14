using Robust.Shared.Prototypes;

namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Data-driven surgery definition for the tutorial Starlight surgery BUI.
/// </summary>
[Prototype]
public sealed partial class TutorialStarlightSurgeryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Display name (plain English for the BUI).</summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public int Priority;

    /// <summary>Virtual part ids this surgery appears under (e.g. Head).</summary>
    [DataField]
    public List<string> Parts = new List<string> { "Head" };

    /// <summary>Surgeries that must already be completed.</summary>
    [DataField]
    public List<ProtoId<TutorialStarlightSurgeryPrototype>> Requirements = new();

    [DataField(required: true)]
    public List<TutorialStarlightSurgeryStepData> Steps = new();

    /// <summary>Hidden once the patient already has an eye implant.</summary>
    [DataField]
    public bool RequiresNoEyeImplant;

    /// <summary>Completing this surgery marks the eye implant as installed.</summary>
    [DataField]
    public bool GrantsEyeImplant;

    /// <summary>Completing this surgery clears incision progress (close incision).</summary>
    [DataField]
    public bool ClearsIncisionProgress;
}

[DataDefinition]
public sealed partial class TutorialStarlightSurgeryStepData
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField(required: true)]
    public TutorialStarlightSurgeryToolType Tool;

    [DataField]
    public float Duration = 1f;
}
