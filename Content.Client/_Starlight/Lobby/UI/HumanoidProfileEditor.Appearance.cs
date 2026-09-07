using System.Linq;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public event Action<List<ProtoId<GuideEntryPrototype>>>? OnOpenGuidebook;

    private ColorSelectorSliders _rgbSkinColorSelector = default!;
    private readonly List<SpeciesPrototype> _species = new();
    private static readonly ProtoId<GuideEntryPrototype> DefaultSpeciesGuidebook = "Species";

    public void RefreshSpecies()
    {
        SpeciesButton.Clear();
        _species.Clear();

        _species.AddRange(_prototypeManager.EnumeratePrototypes<SpeciesPrototype>().Where(o => o.RoundStart));
        _species.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        var speciesIds = _species.Select(o => o.ID).ToList();

        for (var i = 0; i < _species.Count; i++)
        {
            // Far Horizons: subspecies are selected through the subspecies control instead.
            if (_species[i].SubspeciesOf != null)
                continue;

            var name = Loc.GetString(_species[i].Name);
            SpeciesButton.AddItem(name, i);

            if (Profile?.Species.Equals(_species[i].ID) == true ||
                _species.Find(p => p.ID == Profile?.Species)?.SubspeciesOf == _species[i].ID)
            {
                SpeciesButton.SelectId(i);
            }
        }

        if (Profile == null)
            return;

        var parentSpecies = _species.Find(p => p.ID == Profile.Species)?.SubspeciesOf ?? Profile.Species;
        if (!speciesIds.Contains(parentSpecies))
            SetSpecies(SharedHumanoidAppearanceSystem.DefaultSpecies);
    }

    private void SetSpecies(string newSpecies)
    {
        Profile = Profile?.WithSpecies(newSpecies);
        UpdateSubspecies();
        OnSkinColorOnValueChanged();
        Markings.SetSpecies(newSpecies);
        RefreshJobs();
        RefreshAntags();
        RefreshLoadouts();
        UpdateSexControls();
        UpdateSpeciesGuidebookIcon();
        UpdateSizeControls();
        UpdateSpeciesLoadout();
        ReloadPreview();
    }

    private void SetAge(int newAge)
    {
        Profile = Profile?.WithAge(newAge);
        ReloadPreview();
    }

    private void SetSex(Sex newSex)
    {
        Profile = Profile?.WithSex(newSex);

        switch (newSex)
        {
            case Sex.Male:
                Profile = Profile?.WithGender(Gender.Male);
                break;
            case Sex.Female:
                Profile = Profile?.WithGender(Gender.Female);
                break;
            default:
                Profile = Profile?.WithGender(Gender.Epicene);
                break;
        }

        UpdateGenderControls();
        Markings.SetSex(newSex);
        ReloadPreview();
        UpdateVoicesControls();
        UpdateSiliconVoicesControls();
    }

    private void SetGender(Gender newGender)
    {
        Profile = Profile?.WithGender(newGender);
        ReloadPreview();
    }

    private void SetSpawnPriority(SpawnPriorityPreference newSpawnPriority)
    {
        Profile = Profile?.WithSpawnPriorityPreference(newSpawnPriority);
        SetDirty();
    }

    private void SetCustomSpecieName(string customName)
    {
        Profile = Profile?.WithCustomSpecieName(customName);
        SetDirty();
    }

    private void SetCharacterWidth(float newWidth)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithWidth(newWidth));
        UpdateSizeText();
        ReloadProfilePreview();
    }

    private void SetCharacterHeight(float newHeight)
    {
        if (Profile is null)
            return;

        Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithHeight(newHeight));
        UpdateSizeText();
        ReloadProfilePreview();
    }

    private void UpdateGenderControls()
    {
        if (Profile == null)
            return;

        PronounsButton.SelectId((int) Profile.Gender);
    }

    private void UpdateAgeEdit()
    {
        AgeEdit.Text = Profile?.Age.ToString() ?? "";
    }

    private void UpdateSexControls()
    {
        if (Profile == null)
            return;

        SexButton.Clear();

        var sexes = new List<Sex>();
        if (_prototypeManager.Resolve<SpeciesPrototype>(Profile.Species, out var speciesProto))
        {
            foreach (var sex in speciesProto.Sexes)
                sexes.Add(sex);
        }
        else
        {
            sexes.Add(Sex.Unsexed);
        }

        foreach (var sex in sexes)
        {
            SexButton.AddItem(
                Loc.GetString($"humanoid-profile-editor-sex-{sex.ToString().ToLower()}-text"),
                (int) sex);
        }

        if (sexes.Contains(Profile.Sex))
            SexButton.SelectId((int) Profile.Sex);
        else
            SexButton.SelectId((int) sexes[0]);
    }

    private void UpdateEyePickers()
    {
        if (Profile == null)
            return;

        Markings.CurrentEyeColor = Profile.Appearance.EyeColor;
        EyeColorPicker.SetData(Profile.Appearance.EyeColor, Profile.Appearance.EyeGlowing);
    }

    private void UpdateSkinColor()
    {
        if (Profile == null)
            return;

        var skin = _prototypeManager.Index<SpeciesPrototype>(Profile.Species).SkinColoration;
        var strategy = _prototypeManager.Index(skin).Strategy;

        switch (strategy.InputType)
        {
            case SkinColorationStrategyInput.Unary:
                if (!Skin.Visible)
                {
                    Skin.Visible = true;
                    RgbSkinColorContainer.Visible = false;
                }

                Skin.Value = strategy.ToUnary(Profile.Appearance.SkinColor);
                break;

            case SkinColorationStrategyInput.Color:
                if (!RgbSkinColorContainer.Visible)
                {
                    Skin.Visible = false;
                    RgbSkinColorContainer.Visible = true;
                }

                _rgbSkinColorSelector.Color = strategy.ClosestSkinColor(Profile.Appearance.SkinColor);
                break;
        }
    }

    private void UpdateSpawnPriorityControls()
    {
        if (Profile == null)
            return;

        SpawnPriorityButton.SelectId((int) Profile.SpawnPriority);
    }

    private void UpdateCustomSpecieNameEdit()
    {
        if (_species.Count == 0)
            return;

        var species = _species.Find(x => x.ID == Profile?.Species) ?? _species.First();
        CCustomSpecieNameEdit.Text = string.IsNullOrEmpty(Profile?.CustomSpecieName)
            ? Loc.GetString(species.Name)
            : Profile.CustomSpecieName;
        CCustomSpecieName.Visible = species.CustomName;
    }

    private void UpdateSizeControls()
    {
        if (Profile == null)
            return;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var speciesPrototype))
            return;

        WidthSlider.MinValue = speciesPrototype.MinWidth;
        WidthSlider.MaxValue = speciesPrototype.MaxWidth;
        WidthSlider.Value = Profile.Appearance.Width;

        HeightSlider.MinValue = speciesPrototype.MinHeight;
        HeightSlider.MaxValue = speciesPrototype.MaxHeight;
        HeightSlider.Value = Profile.Appearance.Height;

        UpdateSizeText();
    }

    private void UpdateSizeText()
    {
        if (Profile is null)
            return;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var speciesPrototype))
            return;

        var height = speciesPrototype.StandardSize * (Profile.Appearance.Height - 1f) * 2f + speciesPrototype.StandardSize;
        var weight = speciesPrototype.StandardWeight +
                        speciesPrototype.StandardDensity *
                        (Profile.Appearance.Width * Profile.Appearance.Height * Profile.Appearance.Height - 1);

        HeightDescribeLabel.Text = Loc.GetString(
            "humanoid-profile-editor-height-label",
            ("height", Math.Round(height)));
        WidthDescribeLabel.Text = Loc.GetString(
            "humanoid-profile-editor-width-label",
            ("weight", Math.Round(weight, 1)));

        _recordsTab?.UpdateComputedMetrics(Profile);
    }

    public void UpdateSpeciesGuidebookIcon()
    {
        SpeciesInfoButton.StyleClasses.Clear();

        if (_species.Count == 0)
            return;

        var species = Profile?.Species ?? _species.First().ID;
        if (!_prototypeManager.Resolve<SpeciesPrototype>(species, out var speciesProto))
            return;

        if (!_prototypeManager.HasIndex<GuideEntryPrototype>(speciesProto.SubspeciesOf ?? species))
            return;

        SpeciesInfoButton.StyleIdentifier = "SpeciesInfoDefault";
    }

    private void OnSpeciesInfoButtonPressed(BaseButton.ButtonEventArgs args)
    {
        if (_species.Count == 0)
            return;

        var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
        var speciesId = Profile?.Species ?? SharedHumanoidAppearanceSystem.DefaultSpecies;
        var speciesProto = _species.Find(p => p.ID == speciesId) ?? _species.First();
        var species = speciesProto.SubspeciesOf ?? speciesProto.ID;
        var page = DefaultSpeciesGuidebook;

        if (_prototypeManager.HasIndex<GuideEntryPrototype>(species))
            page = new ProtoId<GuideEntryPrototype>(species.Id);

        if (!_prototypeManager.Resolve(DefaultSpeciesGuidebook, out var guideRoot))
            return;

        var dict = new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
        {
            [DefaultSpeciesGuidebook] = guideRoot,
        };
        guidebookController.OpenGuidebook(dict, includeChildren: true, selected: page);
    }

    private void OnSkinColorOnValueChanged()
    {
        if (Profile is null)
            return;

        var skin = _prototypeManager.Index<SpeciesPrototype>(Profile.Species).SkinColoration;
        var strategy = _prototypeManager.Index(skin).Strategy;

        switch (strategy.InputType)
        {
            case SkinColorationStrategyInput.Unary:
                if (!Skin.Visible)
                {
                    Skin.Visible = true;
                    RgbSkinColorContainer.Visible = false;
                }

                var unaryColor = strategy.FromUnary(Skin.Value);
                Markings.CurrentSkinColor = unaryColor;
                Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(unaryColor));
                break;

            case SkinColorationStrategyInput.Color:
                if (!RgbSkinColorContainer.Visible)
                {
                    Skin.Visible = false;
                    RgbSkinColorContainer.Visible = true;
                }

                var color = strategy.ClosestSkinColor(_rgbSkinColorSelector.Color);
                Markings.CurrentSkinColor = color;
                Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(color));
                break;
        }

        ReloadProfilePreview();
    }
}
