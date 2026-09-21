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

        /// Intended for editing another player's preferences.
        PlayerPreferences? TargetPreferences { get; set; }
        /// Pending preferences edited by an admin for viewing before saving.
        PlayerPreferences? PendingPreferences { get; set; }
        /// The target player for whom's preferences are being edited.
        MinimalPlayerInfo? TargetPlayerInfo { get; set; }
        bool ViewingChanges { get; }

        #endregion
    }
}
