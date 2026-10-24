using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared._Starlight.Humanoid;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void OnMarkingChange(MarkingSet markings)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithCharacterAppearance(
            Profile.Appearance.WithMarkings(markings.GetForwardEnumerator().ToList()));
        ReloadProfilePreview();
    }

    private void UpdateMarkings()
    {
        if (Profile == null)
            return;

        Markings.SetData(
            Profile.Appearance.Markings,
            Profile.Species,
            Profile.Sex,
            Profile.Appearance.SkinColor,
            Profile.Appearance.EyeColor);
    }

    private void UpdateHairPickers()
    {
        if (Profile == null)
            return;

        var hairMarking = Profile.Appearance.HairStyleId == HairStyles.DefaultHairStyle
            ? new List<Marking>()
            : new()
            {
                new Marking(
                    Profile.Appearance.HairStyleId,
                    new List<Color> { Profile.Appearance.HairColor },
                    Profile.Appearance.HairGlowing),
            };

        var facialHairMarking = Profile.Appearance.FacialHairStyleId == HairStyles.DefaultFacialHairStyle
            ? new List<Marking>()
            : new()
            {
                new Marking(
                    Profile.Appearance.FacialHairStyleId,
                    new List<Color> { Profile.Appearance.FacialHairColor },
                    Profile.Appearance.FacialHairGlowing),
            };

        HairStylePicker.UpdateData(hairMarking, Profile.Species, 1);
        FacialHairPicker.UpdateData(facialHairMarking, Profile.Species, 1);
    }

    private void UpdateCMarkingsHair()
    {
        if (Profile == null)
            return;

        Color? hairColor = null;
        if (Profile.Appearance.HairStyleId != HairStyles.DefaultHairStyle &&
            _markingManager.Markings.TryGetValue(Profile.Appearance.HairStyleId, out var hairProto) &&
            _markingManager.CanBeApplied(Profile.Species, Profile.Sex, hairProto, _prototypeManager))
        {
            if (_markingManager.MustMatchSkin(
                    Profile.Species,
                    HumanoidVisualLayers.Hair,
                    out _,
                    _prototypeManager))
            {
                hairColor = Profile.Appearance.SkinColor;
            }
            else
            {
                hairColor = Profile.Appearance.HairColor;
            }
        }

        Markings.HairMarking = hairColor != null
            ? new Marking(
                Profile.Appearance.HairStyleId,
                new List<Color> { hairColor.Value },
                Profile.Appearance.HairGlowing)
            : null;
    }

    private void UpdateCMarkingsFacialHair()
    {
        if (Profile == null)
            return;

        Color? facialHairColor = null;
        if (Profile.Appearance.FacialHairStyleId != HairStyles.DefaultFacialHairStyle &&
            _markingManager.Markings.TryGetValue(Profile.Appearance.FacialHairStyleId, out var facialHairProto) &&
            _markingManager.CanBeApplied(Profile.Species, Profile.Sex, facialHairProto, _prototypeManager))
        {
            if (_markingManager.MustMatchSkin(
                    Profile.Species,
                    HumanoidVisualLayers.Hair,
                    out _,
                    _prototypeManager))
            {
                facialHairColor = Profile.Appearance.SkinColor;
            }
            else
            {
                facialHairColor = Profile.Appearance.FacialHairColor;
            }
        }

        Markings.FacialHairMarking = facialHairColor != null
            ? new Marking(
                Profile.Appearance.FacialHairStyleId,
                new List<Color> { facialHairColor.Value },
                Profile.Appearance.FacialHairGlowing)
            : null;
    }

    private void OnCyberneticsUpdated(List<CyberneticImplant> cybernetics)
    {
        Profile = Profile?.WithCybernetics(cybernetics.Select(p => p.ID).ToList());
        ReloadPreview();
    }

    private void UpdateCybernetics()
    {
        if (Profile is null || _species.Count == 0)
            return;

        var species = _species.Find(x => x.ID == Profile.Species) ?? _species.First();
        Cybernetics.SetData(Profile.Cybernetics, species.RoundstartCyberwareCapacity);
    }
}
