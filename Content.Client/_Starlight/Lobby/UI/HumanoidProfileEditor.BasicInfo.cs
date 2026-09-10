using Content.Shared.Preferences;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void SetName(string newName)
    {
        Profile = Profile?.WithName(newName);
        SetDirty();
    }

    private void UpdateNameEdit()
    {
        NameEdit.Text = Profile?.Name ?? "";
    }

    private void RandomizeEverything()
    {
        Profile = HumanoidCharacterProfile.Random();
        SetProfile(Profile, CharacterSlot);
        SetDirty();
    }

    private void RandomizeName()
    {
        if (Profile == null)
            return;

        var name = HumanoidCharacterProfile.GetName(Profile.Species, Profile.Gender);
        SetName(name);
        UpdateNameEdit();
    }
}
