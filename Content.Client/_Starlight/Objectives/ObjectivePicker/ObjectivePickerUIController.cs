using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using Content.Shared.Mind;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface;
using Robust.Client.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Starlight.Objectives.ObjectivePicker;

[UsedImplicitly]
public sealed partial class ObjectivePickerUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;

    private ObjectivePickerWindow? _window;
    [Dependency] private IPlayerManager _players = default!;
    private bool _additionalPending;
    private bool _awaitingOffers;
    private bool _onDisconnect;

    public void OpenPicker()
    {
        var mindSystem = EntityManager.System<SharedMindSystem>();
        if (!mindSystem.TryGetMind(_players.LocalSession, out var mind, out _))
            return;
        if (EntityManager.HasComponent<PotentialObjectivesComponent>(mind))
        {
            EnsureWindow();
            return;
        }
        if (_additionalPending || _awaitingOffers ||
            !EntityManager.TryGetComponent<ObjectivePickerProgressComponent>(mind, out var progress) || !progress.CanPickMore)
            return;

        _additionalPending = true;
        _net.SendSystemNetworkMessage(new ObjectivePickerRequestAdditional { MindId = EntityManager.GetNetEntity(mind) });
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_awaitingOffers || !EntityManager.System<SharedMindSystem>().TryGetMind(_players.LocalSession, out var mind, out _) ||
            !EntityManager.HasComponent<PotentialObjectivesComponent>(mind))
            return;
        _awaitingOffers = false;
        EnsureWindow();
        _characterInfo.RequestCharacterInfo();
    }

    public void OnStateExited(GameplayState state)
    {
        _onDisconnect = true;
        _additionalPending = false;
        _awaitingOffers = false;
        if (_window == null)
            return;

        _window.Close();
        _window = null;
    }

    public void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _onDisconnect = false;
        _window = UIManager.CreateWindow<ObjectivePickerWindow>();
        _window.OpenCentered();
        var mindSystem = EntityManager.System<SharedMindSystem>();
        if (mindSystem.TryGetMind(_players.LocalSession, out var mind, out _))
        {
            var mindId = EntityManager.GetNetEntity(mind);
            _net.SendSystemNetworkMessage(new ObjectivePickerSetOpen { MindId = mindId, Open = true });
            _window.OnClose += () =>
            {
                if (!_onDisconnect)
                    _net.SendSystemNetworkMessage(new ObjectivePickerSetOpen { MindId = mindId, Open = false });
            };
        }
        _window.OnClose += () => _window = null;
        _window.OnSelectedChange += OnSelectedChange;
        _window.OnSubmitted += OnSubmitted;
        _window.OnRandomize += OnRandomize;
        _window.OnClear += () =>
        {
            if (_window == null)
                return;

            _window.SelectedObjectives.Clear();
            _window.UpdateState();
        };
        _window.OnMulligan += OnMulligan;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ObjectivePickerReply>(OnReply);
    }

    private void OnSelectedChange(NetEntity id)
    {
        if (_window == null || _window.Pending || _window.Offers is not { } offers)
            return;
        if (!_window.SelectedObjectives.Remove(id) && ObjectivePickerSelection.Available(offers, id) &&
            ObjectivePickerSelection.Compatible(offers, id, _window.SelectedObjectives))
            _window.SelectedObjectives.Add(id);
        _window.UpdateState();
    }

    private void OnSubmitted(HashSet<NetEntity> selected, NetEntity mind)
    {
        if (_window == null || _window.Pending || _window.Offers is not { } offers ||
            !ObjectivePickerSelection.Valid(offers, selected))
            return;

        _window.SetPending(true);
        _net.SendSystemNetworkMessage(new ObjectivePickerSelected
        {
            MindId = mind,
            SelectedObjectives = new HashSet<NetEntity>(selected),
        });
    }

    private void OnRandomize()
    {
        if (_window == null || _window.Pending || _window.Offers is not { } offers)
            return;
        var order = offers.ObjectiveOptions.Keys.OrderBy(_ => _random.Next()).ToArray();
        if (!ObjectivePickerSelection.TrySelect(offers, order, Array.Empty<NetEntity>(), out var selected))
            return;
        _window.SelectedObjectives.Clear();
        _window.SelectedObjectives.UnionWith(selected);
        _window.UpdateState();
    }

    private void OnMulligan(NetEntity mind)
    {
        if (_window == null || _window.Pending || _window.Offers is not { MulliganUsed: false } offers ||
            _window.SelectedObjectives.Count != 1)
            return;
        var retained = _window.SelectedObjectives.Single();
        if (!ObjectivePickerSelection.Available(offers, retained))
            return;
        _window.SetPending(true);
        _net.SendSystemNetworkMessage(new ObjectivePickerMulligan
        {
            MindId = mind,
            RetainedObjective = retained,
        });
    }

    private void OnReply(ObjectivePickerReply ev, EntitySessionEventArgs args)
    {
        _additionalPending = false;
        if (ev.Accepted && ev.OpenPicker)
        {
            _awaitingOffers = true;
            return;
        }
        if (ev.Accepted && ev.Finished)
        {
            _window?.Close();
            _characterInfo.RequestCharacterInfo();
            return;
        }
        _window?.SetPending(false, ev.Message);
    }
}
