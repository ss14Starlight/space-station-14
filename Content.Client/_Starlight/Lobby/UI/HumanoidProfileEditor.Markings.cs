using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared._Starlight.Humanoid;

namespace Content.Client._Starlight.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private HumanoidBodyEditorBackend _bodyBackend = default!;

    /// <summary>
    /// The body tab draws its own preview of the character, so the side preview hides while it's open.
    /// </summary>
    public bool BodyTabVisible => TabContainer.CurrentTab == BodyTab.GetPositionInParent();

    public event Action<bool>? BodyTabVisibilityChanged;

    private void InitializeBodyTab()
    {
        _bodyBackend = new HumanoidBodyEditorBackend(_prototypeManager, _markingManager, () => Profile);
        _bodyBackend.AppearanceEdited += appearance =>
        {
            if (Profile is null)
                return;

            Profile = Profile.WithCharacterAppearance(appearance);
            ReloadProfilePreview();
        };
        BodyTab.SetBackend(_bodyBackend);

        // The species decides the whole body, so it comes first and is what the tab opens on.
        BodyTab.AddSection(Loc.GetString("body-editor-species"), SpeciesSection, leading: true);
        BodyTab.AddSection(Loc.GetString("humanoid-profile-editor-cybernetics-tab"), CyberneticsSection);
        BodyTab.ShowSection(SpeciesSection);

        TabContainer.OnTabChanged += _ =>
        {
            PreviewPanel.Visible = !BodyTabVisible;
            BodyTabVisibilityChanged?.Invoke(BodyTabVisible);
            // Clothes come off while editing the body.
            ReloadPreview();
        };
    }

    /// <summary>
    /// Reloads the body tab after the species, sex or the whole profile changed.
    /// </summary>
    private void UpdateBodyTab()
    {
        _bodyBackend.Reload();
        BodyTab.ResetSelection();
    }

    /// <summary>
    /// Keeps the skin color in what the species allows, after switching species.
    /// </summary>
    private void ClampSkinColor()
    {
        if (Profile is null || !_prototypeManager.TryIndex(Profile.Species, out var species))
            return;

        var strategy = _prototypeManager.Index(species.SkinColoration).Strategy;
        var skin = strategy.InputType == SkinColorationStrategyInput.Unary
            ? strategy.FromUnary(strategy.ToUnary(Profile.Appearance.SkinColor))
            : strategy.ClosestSkinColor(Profile.Appearance.SkinColor);
        Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithSkinColor(skin));
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
