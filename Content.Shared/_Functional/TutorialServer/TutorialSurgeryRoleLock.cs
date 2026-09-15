namespace Content.Shared._Functional.TutorialServer;

/// <summary>
/// Shared role IDs that hard-lock server-specific surgery Bound UIs to their tutorials.
/// </summary>
public static class TutorialSurgeryRoleLock
{
    public const string StarlightRoleId = "TutorialSurgeryStarlight";
    public const string CyberMedRoleId = "TutorialSurgeryCyberMed";

    public static bool IsInTutorialRole(EntityManager entMan, EntityUid user, string requiredRoleId)
    {
        return entMan.TryGetComponent(user, out TutorialParticipantComponent? participant) &&
               participant.RoleId == requiredRoleId;
    }
}
