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
        PlayerPreferences? Preferences { get; }
        void Initialize();
        void SetCharacterEnable(int slot, bool enable);
        void UpdateCharacter(HumanoidCharacterProfile profile, int slot);
        void CreateCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(HumanoidCharacterProfile profile);
        void DeleteCharacter(int slot);
        void UpdateConstructionFavorites(List<ProtoId<ConstructionPrototype>> favorites);
        void UpdateJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities);
        // Starlight begin
        void SetCharacterEnableForPrefs(int slot, MsgOpenPlayerCharacterSetup playerData, bool enable = true);
        void UpdateCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData, int slot);
        void CreateCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData);
        void DeleteCharacterForPrefs(HumanoidCharacterProfile profile, MsgOpenPlayerCharacterSetup playerData);
        void DeleteCharacterForPrefs(int slot, MsgOpenPlayerCharacterSetup playerData);
        void UpdateJobPrioritiesForPrefs(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities,
            MsgOpenPlayerCharacterSetup playerData);
        // Starlight end
    }
}
