// ReSharper disable CheckNamespace
namespace Content.Shared.Preferences;

public sealed partial class PlayerPreferences
{
    // I cannot think of any conceivable reason not to have this.
    public void SetCharacters(IEnumerable<KeyValuePair<int, HumanoidCharacterProfile>> characters) =>
        _characters = new Dictionary<int, HumanoidCharacterProfile>(characters);
}
