using Robust.Shared.Player;

namespace Content.Server._Starlight.Arcade.Lancer;

[RegisterComponent]
public sealed partial class LancerArcadeComponent : Component
{
    public LancerGame? Game;

    public EntityUid? Player;

    public readonly List<EntityUid> Spectators = new();

    /// <summary>
    /// Hacked via the arcade MNGR wire. When true, the player mech takes no combat damage.
    /// </summary>
    [ViewVariables]
    public bool PlayerInvincible;

    /// <summary>Number of unique missions cleared on this cabinet (0–3).</summary>
    public int LicenseLevel;

    /// <summary>Permanent Hull skill points on this cabinet.</summary>
    public int Hull;

    /// <summary>Permanent Agility skill points on this cabinet.</summary>
    public int Agility;

    /// <summary>Permanent Engineering skill points on this cabinet.</summary>
    public int Engineering;

    /// <summary>True after crown-signal has been cleared once (campaign win already awarded).</summary>
    public bool CampaignCompleted;

    /// <summary>Mission ids available on this cabinet for the current campaign.</summary>
    public HashSet<string> UnlockedMissionIds = ["ridge-pass"];

    /// <summary>Mission ids cleared on this cabinet during the current campaign.</summary>
    public HashSet<string> ClearedMissionIds = [];

    public void ResetCampaign()
    {
        LicenseLevel = 0;
        Hull = 0;
        Agility = 0;
        Engineering = 0;
        CampaignCompleted = false;
        UnlockedMissionIds = ["ridge-pass"];
        ClearedMissionIds = [];
    }
}
