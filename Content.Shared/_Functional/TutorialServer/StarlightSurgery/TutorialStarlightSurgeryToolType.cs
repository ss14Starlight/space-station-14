namespace Content.Shared._Functional.TutorialServer.StarlightSurgery;

/// <summary>
/// Tool / implant kinds required by tutorial Starlight surgery steps.
/// Mirrors Starlight's component-matched tools without forking the body system.
/// </summary>
public enum TutorialStarlightSurgeryToolType : byte
{
    Scalpel,
    Hemostat,
    Retractor,
    Cautery,
    BoneGel,
    EyeImplant,
}
