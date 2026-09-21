using Content.Shared._Starlight.Traits;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void OnTraitsSelectionChanged(HashSet<ProtoId<TraitPrototype>> traits)
    {
        if (Profile is null)
            return;

        foreach (var existingTrait in Profile.TraitPreferences)
            Profile = Profile.WithoutTraitPreference(existingTrait, _prototypeManager);

        foreach (var trait in traits)
            Profile = Profile.WithTraitPreference(trait.Id, _prototypeManager);

        SetDirty();
    }

    private void UpdateTraitsSelection()
    {
        if (Profile is null)
        {
            Traits.SetSelectedTraits(new HashSet<ProtoId<TraitPrototype>>(), Profile);
            return;
        }

        var selectedTraits = new HashSet<ProtoId<TraitPrototype>>(Profile.TraitPreferences.Count);
        foreach (var traitId in Profile.TraitPreferences)
        {
            if (_prototypeManager.HasIndex(traitId))
                selectedTraits.Add(new ProtoId<TraitPrototype>(traitId));
        }

        Traits.SetSelectedTraits(selectedTraits, Profile);
        Traits.UpdateRequirements(Profile);
    }
}
