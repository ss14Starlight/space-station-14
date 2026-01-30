using System.Numerics;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.NewLife;

public sealed partial class NewLifeWindow : DefaultWindow
{
    private readonly IClientPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    private readonly Dictionary<NetEntity, Dictionary<string, List<JobButton>>> _jobButtons = new();
    private readonly Dictionary<NetEntity, Dictionary<string, BoxContainer>> _jobCategories = new();
    private HashSet<int> _usedSlots = [];
    private int _remainingLives = 5;
    private int _maxLives = 5;
    private TimeSpan _lastGhostTime = TimeSpan.Zero;
    private TimeSpan _cooldown = TimeSpan.Zero;

    public readonly LateJoinGui LateJoinGui = default!;

    public NewLifeWindow(IClientPreferencesManager preferencesManager)
    {
        _preferencesManager = preferencesManager;

        SetSize = new Vector2(685, 560);
        MinSize = new Vector2(685, 560);
        LateJoinGui = new LateJoinGui();
        IoCManager.InjectDependencies(this);
        UpdateTitle();

        LateJoinGui.Contents.Orphan();
        LateJoinGui.Contents.Margin = new Thickness(0, 25, 0, 0);
        LateJoinGui.SelectedId += (_) => Close();
        AddChild(LateJoinGui.Contents);
        _jobRequirements.Updated += RemoveUsedCharacters;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _jobRequirements.Updated -= RemoveUsedCharacters;
            _jobButtons.Clear();
            _jobCategories.Clear();
        }
    }

    private void RemoveUsedCharacters()
    {
        if (_preferencesManager.Preferences is null)
            return;
        foreach (var control in LateJoinGui.CharList.Children)
        {
            if (control is not CharacterPickerButton pickerButton)
                continue;

            pickerButton.Disabled = _usedSlots.Contains(_preferencesManager.Preferences.IndexOfCharacter(pickerButton.Profile));
            //if we have no lives left, disable all buttons
            if (_remainingLives <= 0)
            {
                pickerButton.Disabled = true;
            }

            //if we are on cooldown, disable all buttons
            if (_lastGhostTime + _cooldown > _gameTiming.CurTime)
            {
                pickerButton.Disabled = true;
            }

            if (pickerButton is { Disabled: true, Pressed: true })
            {
                pickerButton.Pressed = false;
            }
        }
    }

    //force update the title text
    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateTitle();
        RemoveUsedCharacters();
    }

    private void UpdateTitle()
    {
        //decide between normal title and cooldown title
        if (_lastGhostTime + _cooldown > _gameTiming.CurTime)
        {
            var timeLeft = (_lastGhostTime + _cooldown) - _gameTiming.CurTime;
            Title = Loc.GetString("ghost-new-life-window-title-cooldown", ("time", timeLeft.ToString(@"hh\:mm\:ss")));
        }
        else
        {
            Title = Loc.GetString("ghost-new-life-window-title", ("remainingLives", _remainingLives), ("maxLives", _maxLives));
        }
    }

    public void ReloadUI(HashSet<int> usedSlots, int remainingLives, int maxLives, TimeSpan lastGhostTime, TimeSpan cooldown)
    {
        _usedSlots = usedSlots;
        _remainingLives = remainingLives;
        _maxLives = maxLives;
        _lastGhostTime = lastGhostTime;
        _cooldown = cooldown;
        RemoveUsedCharacters();
        UpdateTitle();
    }
}
