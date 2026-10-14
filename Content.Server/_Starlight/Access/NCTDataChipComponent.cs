using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight.Access;

[RegisterComponent]
public sealed partial class NCTDataChipComponent : Component
{
    [DataField]
    public string Trainee = "";

    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> BlacklistTags = ["CentralCommand", "Captain", "HeadOfPersonnel", "EmergencyShuttleRepealAll", "Cryogenics", "ChiefEngineer", "ChiefMedicalOfficer", "ResearchDirector", "HeadOfSecurity", "Armory", "NuclearOperative", "SyndicateAgent", "Wizard"];
}
