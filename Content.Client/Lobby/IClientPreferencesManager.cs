using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby
{
    public interface IClientPreferencesManager
    {
        event Action OnServerDataLoaded;

        bool ServerDataLoaded => Settings != null;

        GameSettings? Settings { get; }
        PlayerPreferences Preferences { get; set; } // Starlight edit
        void Initialize();
        void SetCharacterEnable(int slot, bool enable);
        void UpdateCharacter(HumanoidCharacterProfile profile, int slot);
        void CreateCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(int slot);
        void UpdateConstructionFavorites(List<ProtoId<ConstructionPrototype>> favorites);
        void UpdateJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities);

        #region Starlight

        /// <summary>
        /// Intended for editing another player's preferences.
        /// </summary>
        PlayerPreferences? TargetPreferences { get; set; }
        /// <summary>
        /// Pending preferences edited by an admin for viewing before saving.
        /// </summary>
        PlayerPreferences? PendingPreferences { get; set; }
        /// <summary>
        /// The target player for whom's preferences are being edited.
        /// </summary>
        MinimalPlayerInfo? TargetPlayerInfo { get; set; }
        /// <summary>
        /// While reviewing, whether <see cref="IClientPreferencesManager.Preferences"/> shows the pending edits or the current preferences.
        /// </summary>
        bool ViewingChanges { get; set; }
        /// <summary>
        /// Whether the pending edits are being reviewed. Preferences cannot be modified while this is true.
        /// </summary>
        bool ReviewingPending { get; }
        /// <summary>
        /// The last admin to edit <see cref="PendingPreferences"/>.
        /// </summary>
        MinimalPlayerInfo? PendingPreferencesAdmin { get; }
        /// <summary>
        /// Raised when the server sends pending preference edits to review.
        /// </summary>
        event Action OnPendingPreferencesReceived;
        /// <summary>
        /// Accept the pending preference edits, applying them to your preferences.
        /// </summary>
        void AcceptPendingPreferences();
        /// <summary>
        /// Reject the pending preference edits, discarding them.
        /// </summary>
        void RejectPendingPreferences(string? reason = null);
        void StartPendingReview();
        void EndPendingReview();

        #endregion
    }
}
