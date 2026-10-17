using System.Linq;
using Content.Shared._Starlight.Player;
using Content.Shared._Starlight.Preferences;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client;
using Robust.Client.Player;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Lobby
{
    /// <summary>
    ///     Receives <see cref="PlayerPreferences" /> and <see cref="GameSettings" /> from the server during the initial
    ///     connection.
    ///     Stores preferences on the server through <see cref="SelectCharacter" /> and <see cref="UpdateCharacter" />.
    /// </summary>
    public sealed partial class ClientPreferencesManager : IClientPreferencesManager
    {
        [Dependency] private IClientNetManager _netManager = default!;
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IPlayerManager _playerManager = default!;

        public event Action? OnServerDataLoaded;

        public GameSettings Settings { get; private set; } = default!;
        // Starlight begin
        private PlayerPreferences _preferences = null!;

        public PlayerPreferences Preferences
        {
            get
            {
                if (ReviewingPending)
                    return ViewingChanges ? PendingPreferences ?? _preferences : _preferences;
                return TargetPreferences ?? _preferences;
            }
            set
            {
                if (ReviewingPending) return;
                if (TargetPreferences is not null) TargetPreferences = value;
                else _preferences = value;
            }
        }
        // Starlight end

        public void Initialize()
        {
            _netManager.RegisterNetMessage<MsgPreferencesAndSettings>(HandlePreferencesAndSettings);
            _netManager.RegisterNetMessage<MsgHasPendingPreferenceUpdates>(HandlePendingPreferencesUpdate); // Starlight
            _netManager.RegisterNetMessage<MsgForceUpdatePlayerCharacter>(); // Starlight
            _netManager.RegisterNetMessage<MsgForceDeletePlayerCharacter>(); // Starlight
            _netManager.RegisterNetMessage<MsgForcePlayerCharacterEnable>(); // Starlight
            _netManager.RegisterNetMessage<MsgForceUpdatePlayerJobPriorities>(); // Starlight
            _netManager.RegisterNetMessage<MsgPreferenceConfirmationResult>(); // Starlight
            _netManager.RegisterNetMessage<MsgUpdateCharacter>();
            _netManager.RegisterNetMessage<MsgDeleteCharacter>();
            _netManager.RegisterNetMessage<MsgSetCharacterEnable>();

            _baseClient.RunLevelChanged += BaseClientOnRunLevelChanged;
        }

        private void BaseClientOnRunLevelChanged(object? sender, RunLevelChangedEventArgs e)
        {
            if (e.NewLevel == ClientRunLevel.Initialize)
            {
                Settings = default!;
                // Starlight begin
                EndPendingReview();
                PendingPreferences = null;
                PendingPreferencesAdmin = null;
                _preferences = default!;
                // Starlight end
            }
        }

        /// <summary>
        /// Send message to server to enable or disable a character in a slot
        /// </summary>
        /// <param name="slot">slot number to modify</param>
        /// <param name="enable">whether to enable or disable the character</param>
        public void SetCharacterEnable(int slot, bool enable = true)
        {
            if (ReviewingPending) return; // Starlight
            if (!Preferences.Characters.TryGetValue(slot, out var characterProfile))
                return;
            if (characterProfile is not HumanoidCharacterProfile profile)
                return;

            var characters = new Dictionary<int, HumanoidCharacterProfile>(Preferences.Characters)
            {
                [slot] = new HumanoidCharacterProfile(profile) {Enabled = enable},
            };
            Preferences = new PlayerPreferences(characters, Preferences.AdminOOCColor, Preferences.ConstructionFavorites, Preferences.JobPriorities);

            // Starlight begin
            NetMessage msg = TargetPreferences is null
                ? new MsgSetCharacterEnable { CharacterIndex = slot, EnabledValue = enable, }
                : new MsgForcePlayerCharacterEnable
                {
                    CharacterIndex = slot,
                    EnabledValue = enable,
                    UserId = TargetPlayerInfo!.UserId,
                    NotifyPlayer = true
                };
            // Starlight end
            _netManager.ClientSendMessage(msg);
        }

        public void UpdateCharacter(HumanoidCharacterProfile profile, int slot)
        {
            if (ReviewingPending) return; // Starlight
            var collection = IoCManager.Instance!;
            profile.EnsureValid(_playerManager.LocalSession!, collection);
            var characters = new Dictionary<int, HumanoidCharacterProfile>(Preferences.Characters) {[slot] = profile};
            Preferences = new PlayerPreferences(characters, Preferences.AdminOOCColor, Preferences.ConstructionFavorites, Preferences.JobPriorities);
            // Starlight begin
            NetMessage msg = TargetPreferences is null
                ? new MsgUpdateCharacter { Profile = profile, Slot = slot }
                : new MsgForceUpdatePlayerCharacter
                {
                    Profile = profile, Slot = slot, UserId = TargetPlayerInfo!.UserId, NotifyPlayer = true
                };
            // Starlight end
            _netManager.ClientSendMessage(msg);
        }

        public void CreateCharacter(HumanoidCharacterProfile profile)
        {
            if (ReviewingPending) return; // Starlight
            var characters = new Dictionary<int, HumanoidCharacterProfile>(Preferences.Characters);
            var lowest = Enumerable.Range(0, Settings.MaxCharacterSlots)
                .Except(characters.Keys)
                .FirstOrNull();

            if (lowest == null)
            {
                throw new InvalidOperationException("Out of character slots!");
            }

            var l = lowest.Value;
            characters.Add(l, profile);
            Preferences = new PlayerPreferences(characters, Preferences.AdminOOCColor, Preferences.ConstructionFavorites, Preferences.JobPriorities);

            UpdateCharacter(profile, l);
        }

        public void DeleteCharacter(HumanoidCharacterProfile profile)
        {
            DeleteCharacter(Preferences.IndexOfCharacter(profile));
        }

        public void DeleteCharacter(int slot)
        {
            if (ReviewingPending) return; // Starlight
            var characters = Preferences.Characters.Where(p => p.Key != slot);
            Preferences = new PlayerPreferences(characters, Preferences.AdminOOCColor, Preferences.ConstructionFavorites, Preferences.JobPriorities);
            // Starlight begin
            NetMessage msg = TargetPreferences is null
                ? new MsgDeleteCharacter { Slot = slot }
                : new MsgForceDeletePlayerCharacter
                {
                    Slot = slot, UserId = TargetPlayerInfo!.UserId, NotifyPlayer = true,
                };
            _netManager.ClientSendMessage(msg);
            // Starlight end
        }

        /// <summary>
        /// Send a message to the server to update the list of your job priorities
        /// </summary>
        /// <param name="jobPriorities">Dictionary of job priorities to save to the server</param>
        public void UpdateJobPriorities(Dictionary<ProtoId<JobPrototype>, JobPriority> jobPriorities)
        {
            if (ReviewingPending) return; // Starlight
            Preferences = new PlayerPreferences(Preferences.Characters,
                Preferences.AdminOOCColor,
                Preferences.ConstructionFavorites,
                jobPriorities);
            // Starlight begin
            NetMessage msg = TargetPreferences is null
                ? new MsgUpdateJobPriorities { JobPriorities = jobPriorities, }
                : new MsgForceUpdatePlayerJobPriorities
                {
                    JobPriorities = jobPriorities, UserId = TargetPlayerInfo!.UserId, NotifyPlayer = true
                };
            // Starlight end
            _netManager.ClientSendMessage(msg);
        }

        public void UpdateConstructionFavorites(List<ProtoId<ConstructionPrototype>> favorites)
        {
            _preferences = new PlayerPreferences(_preferences.Characters, _preferences.AdminOOCColor, favorites, _preferences.JobPriorities); // Starlight edit
            var msg = new MsgUpdateConstructionFavorites
            {
                Favorites = favorites
            };
            _netManager.ClientSendMessage(msg);
        }

        private void HandlePreferencesAndSettings(MsgPreferencesAndSettings message)
        {
            _preferences = message.Preferences; // Starlight edit
            Settings = message.Settings;

            OnServerDataLoaded?.Invoke();
        }
    }
}
