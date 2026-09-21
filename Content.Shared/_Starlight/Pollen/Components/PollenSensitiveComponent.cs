[RegisterComponent]
public sealed partial class PollenSensitiveComponent : Component
{
[DataField]
public float InteractionChance = 0.2f;

[DataField]
public float PollenRange = 1f;

[DataField]
public float AllergyBuildup = 1f;

[DataField]
public float AllergyDecay = 0.10f;

[DataField]
public float SneezeAmount = 0.20f;

[DataField]
public float HistamineAmount = 0.5f;

[DataField]
public TimeSpan NextHistamine;

[DataField]
public float HistamineBuildupReduction = 0.5f;

[DataField]
public float AllergyStack;
public TimeSpan NextInteraction;
public TimeSpan NextAllergyUpdate;
public TimeSpan NextSneeze;
public bool NoseItchActive;
public bool SneezingActive;
public bool HistamineActive;
public bool SevereAllergyActive;
}
