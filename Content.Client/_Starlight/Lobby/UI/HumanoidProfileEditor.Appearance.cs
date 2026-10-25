using System.Globalization;
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
            SpeciesButton.AddItem(name, i, _species[i].DollPrototype, SpeciesSummary(_species[i]), // Starlight-edit: cards with a preview
                _prototypeManager.HasIndex<GuideEntryPrototype>(_species[i].ID));

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
        ClampSkinColor();
        UpdateBodyTab();
        RefreshJobs();
        RefreshAntags();
        RefreshLoadouts();
        UpdateSexControls();
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
        UpdateBodyTab();
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

    /// <param name="forceEdits">Rewrite the number boxes even while one has focus, after a value typed in was applied.</param>
    private void UpdateSizeText(bool forceEdits = false)
    {
        if (Profile is null)
            return;

        if (!_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var speciesPrototype))
            return;

        var (height, weight) = SizeToMetrics(speciesPrototype, Profile.Appearance.Height, Profile.Appearance.Width);

        // Not while typing, the slider moving under the caret would rewrite the number.
        if (forceEdits || !HeightEdit.HasKeyboardFocus())
            HeightEdit.Text = Math.Round(height).ToString(CultureInfo.InvariantCulture);
        if (forceEdits || !WeightEdit.HasKeyboardFocus())
            WeightEdit.Text = Math.Round(weight).ToString(CultureInfo.InvariantCulture);

        _recordsTab?.UpdateComputedMetrics(Profile);
    }

    /// <summary>
    /// Height in centimeters and weight in kilograms for the size scales, the same way character records count them.
    /// </summary>
    private static (float Height, float Weight) SizeToMetrics(SpeciesPrototype species, float heightScale, float widthScale)
    {
        var height = species.StandardSize * (heightScale - 1f) * 2f + species.StandardSize;
        var weight = species.StandardWeight + species.StandardDensity * (widthScale * heightScale * heightScale - 1f);
        return (height, weight);
    }

    /// <summary>
    /// Sets the height from centimeters typed in, kept within what the species allows.
    /// </summary>
    private void OnHeightEntered(string text)
    {
        if (Profile is null || !_prototypeManager.TryIndex(Profile.Species, out var species))
            return;

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
        {
            var scale = (height - species.StandardSize) / (2f * species.StandardSize) + 1f;
            HeightSlider.Value = Math.Clamp(scale, species.MinHeight, species.MaxHeight);
        }

        UpdateSizeText(forceEdits: true);
    }

    /// <summary>
    /// Sets the build from kilograms typed in at the current height, kept within what the species allows.
    /// </summary>
    private void OnWeightEntered(string text)
    {
        if (Profile is null || !_prototypeManager.TryIndex(Profile.Species, out var species))
            return;

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) && species.StandardDensity != 0)
        {
            var heightScale = Profile.Appearance.Height;
            var scale = ((weight - species.StandardWeight) / species.StandardDensity + 1f) / (heightScale * heightScale);
            WidthSlider.Value = Math.Clamp(scale, species.MinWidth, species.MaxWidth);
        }

        UpdateSizeText(forceEdits: true);
    }

    /// <summary>
    /// A few lines about the species for the tooltip of its card.
    /// </summary>
    private string SpeciesSummary(SpeciesPrototype species)
    {
        var lines = new List<string> { Loc.GetString(species.Name) };
        if (Loc.TryGetString($"species-summary-{species.ID.ToLowerInvariant()}", out var summary))
            lines.Add(summary);

        lines.Add(Loc.GetString("humanoid-profile-editor-species-summary-stats",
            ("minAge", species.MinAge),
            ("maxAge", species.MaxAge),
            ("height", species.StandardSize),
            ("weight", species.StandardWeight)));
        return string.Join(Environment.NewLine, lines);
    }

    private void OpenSpeciesGuidebook(SpeciesPrototype speciesProto)
    {
        var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
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
}
