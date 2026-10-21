using System.Linq;
using System.Runtime.InteropServices;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared._Starlight.Admeme.Jumpscare;
using Content.Shared._Starlight.Commands;
using Content.Shared.Administration;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;

// TODO: Come back and improve this when I add nullable parsing to toolshed.
namespace Content.Server._Starlight.Admeme.Jumpscare;

[ToolshedCommand]
[AdminCommand(AdminFlags.Mapping)] // Eventmin-only.
public sealed class JumpscareCommand : ToolshedCommand
{
    private JumpscareSystem? _jumpscare;
    private EuiManager? _eui;

    private ICommonSession Jumpscare(ICommonSession session, float holdTime, float fadeTime, bool aboveUI,
        SpriteSpecifier sprite, string audioPath, bool isCollection, float volume, float pitch, float maxDist,
        float rolloff, float refDist, float playOffset, float variation)
    {
        _jumpscare ??= GetSys<JumpscareSystem>();

        var @params = new AudioParams
        {
            Volume = volume,
            Pitch = pitch,
            MaxDistance = maxDist,
            PlayOffsetSeconds = playOffset,
            ReferenceDistance = refDist,
            RolloffFactor = rolloff,
            Variation = variation
        };

        SoundSpecifier? sound = audioPath == string.Empty ? null
            : isCollection
            ? new SoundCollectionSpecifier(audioPath, @params)
            : new SoundPathSpecifier(new ResPath(audioPath), @params);

        var jumpscare = new JumpscareData
        {
            HoldTime = TimeSpan.FromSeconds(holdTime),
            FadeTime = TimeSpan.FromSeconds(fadeTime),
            Sprite = sprite,
            Sound = sound,
            RenderAboveUI = aboveUI
        };

        _jumpscare.Jumpscare(session, jumpscare);

        return session;
    }

    [CommandImplementation("withrsi")]
    public ICommonSession JumpscareRsi([PipedArgument] ICommonSession session, float holdTime, float fadeTime,
        bool aboveUI, ResPath rsiPath, string state, [Optional] [DefaultParameterValue("")] string audioPath,
        [Optional] [DefaultParameterValue(false)]
        bool isCollection, [Optional] [DefaultParameterValue(0f)] float volume,
        [Optional] [DefaultParameterValue(1f)] float pitch,
        [Optional] [DefaultParameterValue(SharedAudioSystem.DefaultSoundRange)]
        float maxDist,
        [Optional] [DefaultParameterValue(1f)] float rolloff, [Optional] [DefaultParameterValue(1f)] float refDist,
        [Optional] [DefaultParameterValue(0f)] float playOffset,
        [Optional] [DefaultParameterValue(0f)] float variation) =>
        Jumpscare(session, holdTime, fadeTime, aboveUI, new SpriteSpecifier.Rsi(rsiPath, state), audioPath,
            isCollection, volume, pitch, maxDist, rolloff, refDist, playOffset, variation);

    [CommandImplementation("withtex")]
    public ICommonSession JumpscareTex([PipedArgument] ICommonSession session, float holdTime, float fadeTime,
        bool aboveUI, ResPath texPath, [Optional] [DefaultParameterValue("")] string audioPath,
        [Optional] [DefaultParameterValue(false)]
        bool isCollection, [Optional] [DefaultParameterValue(0f)] float volume,
        [Optional] [DefaultParameterValue(1f)] float pitch,
        [Optional] [DefaultParameterValue(SharedAudioSystem.DefaultSoundRange)]
        float maxDist,
        [Optional] [DefaultParameterValue(1f)] float rolloff, [Optional] [DefaultParameterValue(1f)] float refDist,
        [Optional] [DefaultParameterValue(0f)] float playOffset,
        [Optional] [DefaultParameterValue(0f)] float variation) =>
        Jumpscare(session, holdTime, fadeTime, aboveUI, new SpriteSpecifier.Texture(texPath), audioPath, isCollection,
            volume, pitch, maxDist, rolloff, refDist, playOffset, variation);

    [CommandImplementation("withrsi")]
    public IEnumerable<ICommonSession> JumpscareRsi([PipedArgument] IEnumerable<ICommonSession> session, float holdTime,
        float fadeTime,
        bool aboveUI, ResPath rsiPath, string state, [Optional] [DefaultParameterValue("")] string audioPath,
        [Optional] [DefaultParameterValue(false)]
        bool isCollection, [Optional] [DefaultParameterValue(0f)] float volume,
        [Optional] [DefaultParameterValue(1f)] float pitch,
        [Optional] [DefaultParameterValue(SharedAudioSystem.DefaultSoundRange)]
        float maxDist,
        [Optional] [DefaultParameterValue(1f)] float rolloff, [Optional] [DefaultParameterValue(1f)] float refDist,
        [Optional] [DefaultParameterValue(0f)] float playOffset,
        [Optional] [DefaultParameterValue(0f)] float variation) =>
        session.Select(x => Jumpscare(x, holdTime, fadeTime, aboveUI, new SpriteSpecifier.Rsi(rsiPath, state),
            audioPath, isCollection, volume, pitch, maxDist, rolloff, refDist, playOffset, variation));

    [CommandImplementation("withtex")]
    public IEnumerable<ICommonSession> JumpscareTex([PipedArgument] IEnumerable<ICommonSession> session, float holdTime,
        float fadeTime,
        bool aboveUI, ResPath texPath, [Optional] [DefaultParameterValue("")] string audioPath,
        [Optional] [DefaultParameterValue(false)]
        bool isCollection, [Optional] [DefaultParameterValue(0f)] float volume,
        [Optional] [DefaultParameterValue(1f)] float pitch,
        [Optional] [DefaultParameterValue(SharedAudioSystem.DefaultSoundRange)]
        float maxDist,
        [Optional] [DefaultParameterValue(1f)] float rolloff, [Optional] [DefaultParameterValue(1f)] float refDist,
        [Optional] [DefaultParameterValue(0f)] float playOffset,
        [Optional] [DefaultParameterValue(0f)] float variation) =>
        session.Select(x => Jumpscare(x, holdTime, fadeTime, aboveUI, new SpriteSpecifier.Texture(texPath), audioPath,
            isCollection, volume, pitch, maxDist, rolloff, refDist, playOffset, variation));

    [CommandImplementation("openpanel")]
    public void OpenPanel(IInvocationContext ctx)
    {
        if (SharedCommandHelpers.NoSession(ctx))
            return;
        _eui ??= IoCManager.Resolve<EuiManager>();
        _eui.OpenEui(new JumpscarePanelEui(), ctx.Session!);
    }

    [CommandImplementation("openpanel")]
    public EntityUid OpenPanel(IInvocationContext ctx, [PipedArgument] EntityUid uid)
    {
        if (SharedCommandHelpers.NoSession(ctx))
            return uid;
        _eui ??= IoCManager.Resolve<EuiManager>();
        _eui.OpenEui(new JumpscarePanelEui(uid), ctx.Session!);
        return uid;
    }
}
