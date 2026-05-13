using Content.Client.UserInterface.Controls;
using Content.Shared._Starlight.Radio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Content.Shared.Radio.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.Player;

namespace Content.Client._Starlight.Radio.UI;

[UsedImplicitly]
public sealed class EncryptionKeyHolderBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly ISharedPlayerManager _playerManager = default!;
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

        foreach (var key in comp.KeyContainer.ContainedEntities)
        {
            if (!EntMan.TryGetComponent<EncryptionKeyComponent>(key, out var keyComp))
                continue;

            foreach (var channel in keyComp.Channels)
            {
                var locString = Loc.GetString("encryption-key-mute");
                if (!_protoManager.TryIndex<RadioChannelPrototype>(channel, out var channelPrototype))
                    continue;

                var button = new RadialMenuActionOption<RadioChannelPrototype>(HandleRadialMenuClick, channelPrototype)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(key),
                    ToolTip = $"{locString} {channelPrototype.LocalizedName}",
                    BackgroundColor = channelPrototype.Color.WithAlpha(128)
                };
                options.Add(button);
            }

            foreach (var channel in keyComp.MutedChannels)
            {
                var locString = Loc.GetString("encryption-key-unmute");
                if (!_protoManager.TryIndex<RadioChannelPrototype>(channel, out var channelPrototype))
                    continue;

                var button = new RadialMenuActionOption<RadioChannelPrototype>(HandleRadialMenuClick, channelPrototype)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(key),
                    ToolTip = $"{locString} {channelPrototype.LocalizedName}",
                    BackgroundColor = channelPrototype.Color.WithAlpha(128)
                };
                options.Add(button);
            }
        }

        return options;
    }

    private void HandleRadialMenuClick(RadioChannelPrototype proto)
    {
        SendPredictedMessage(new EncryptionKeyToggleMessage(proto.ID));
    }
}
