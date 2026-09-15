using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Radio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Radio.UI;

[UsedImplicitly]
public sealed partial class EncryptionKeyHolderBoundUserInterface : BoundUserInterface
{
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    private SimpleRadialMenu? _menu;

    public EncryptionKeyHolderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) => IoCManager.InjectDependencies(this);

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<EncryptionKeyHolderComponent>(Owner, out var holderComp))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var channels = ConvertToButtons(holderComp);
        _menu.SetButtons(channels);
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(EncryptionKeyHolderComponent comp)
    {
        var options = new List<RadialMenuOptionBase>();

        var combinedList = new Dictionary<ProtoId<RadioChannelPrototype>, ChannelState>();

        foreach (var key in comp.KeyContainer.ContainedEntities)
        {
            if (!EntMan.TryGetComponent<EncryptionKeyComponent>(key, out var keyComp))
                continue;

            foreach (var channel in keyComp.Channels) combinedList.TryAdd(channel, ChannelState.Enabled);

            foreach (var channel in keyComp.MutedChannels) combinedList.TryAdd(channel, ChannelState.Muted);
        }

        foreach (var channel in combinedList)
        {
            if (channel.Value == ChannelState.Enabled)
            {
                var locString = Loc.GetString("encryption-key-mute");
                if (!_protoManager.TryIndex<RadioChannelPrototype>(channel.Key, out var channelPrototype))
                    continue;

                var button = new RadialMenuActionOption<RadioChannelPrototype>(HandleRadialMenuClick, channelPrototype)
                {
                    ToolTip = $"{locString} {channelPrototype.LocalizedName}",
                    BackgroundColor = channelPrototype.Color.WithAlpha(128),
                    IconSpecifier = RadialMenuIconSpecifier.With(channelPrototype.Icon)
                };

                options.Add(button);
            }

            if (channel.Value == ChannelState.Muted)
            {
                var locString = Loc.GetString("encryption-key-unmute");
                if (!_protoManager.TryIndex<RadioChannelPrototype>(channel.Key, out var channelPrototype))
                    continue;

                var button = new RadialMenuActionOption<RadioChannelPrototype>(HandleRadialMenuClick, channelPrototype)
                {
                    ToolTip = $"{locString} {channelPrototype.LocalizedName}",
                    BackgroundColor = channelPrototype.Color.WithAlpha(32),
                    IconSpecifier = RadialMenuIconSpecifier.With(channelPrototype.Icon)
                };

                options.Add(button);
            }
        }

        return options;
    }

    private void HandleRadialMenuClick(RadioChannelPrototype proto) => SendPredictedMessage(new EncryptionKeyToggleMessage(proto.ID));

    private enum ChannelState
    {
        Enabled,
        Muted,
    }
}
