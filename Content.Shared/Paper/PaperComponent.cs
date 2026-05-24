using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaperComponent : Component
{
    public PaperAction Mode;

    [DataField("content"), AutoNetworkedField]
    public string Content { get; set; } = "";

    [DataField("contentSize")]
    public int ContentSize { get; set; } = 10000;

    [DataField("stampedBy"), AutoNetworkedField]
    public List<StampDisplayInfo> StampedBy { get; set; } = new();

    /// <summary>
    ///     Stamp to be displayed on the paper, state from bureaucracy.rsi
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string? StampState { get; set; }

    [DataField, AutoNetworkedField]
    public bool EditingDisabled;

    /// <summary>
    /// Drawing data saved on this paper.
    /// Stored as a simple string instead of nested lists to avoid NetSerializer issues.
    /// Format:
    /// stroke|stroke|stroke
    /// stroke = x,y;x,y;x,y
    /// x/y are normalized paper coordinates from 0 to 1.
    /// </summary>
    [DataField("drawingData"), AutoNetworkedField]
    public string DrawingData { get; set; } = "";

    /// <summary>
    /// Maximum encoded drawing string length allowed on one paper.
    /// </summary>
    [DataField("maxDrawingDataLength")]
    public int MaxDrawingDataLength = 20000;

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly PaperAction Mode;
        public readonly string DrawingData;

        public PaperBoundUserInterfaceState(
            string text,
            List<StampDisplayInfo> stampedBy,
            string drawingData,
            PaperAction mode = PaperAction.Read)
        {
            Text = text;
            StampedBy = stampedBy;
            DrawingData = drawingData;
            Mode = mode;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputTextMessage : BoundUserInterfaceMessage
    {
        public readonly string Text;

        public PaperInputTextMessage(string text)
        {
            Text = text;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputDrawingMessage : BoundUserInterfaceMessage
    {
        public readonly string DrawingData;

        public PaperInputDrawingMessage(string drawingData)
        {
            DrawingData = drawingData;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperClearDrawingMessage : BoundUserInterfaceMessage
    {
    }

    // Starlight-start
    [Serializable, NetSerializable]
    public sealed class PaperSignatureRequestMessage : BoundUserInterfaceMessage
    {
        public readonly int SignatureIndex;

        public PaperSignatureRequestMessage(int signatureIndex)
        {
            SignatureIndex = signatureIndex;
        }
    }
    // Starlight-end

    [Serializable, NetSerializable]
    public enum PaperUiKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public enum PaperAction
    {
        Read,
        Write,
    }

    [Serializable, NetSerializable]
    public enum PaperVisuals : byte
    {
        Status,
        Stamp
    }

    [Serializable, NetSerializable]
    public enum PaperStatus : byte
    {
        Blank,
        Written
    }
}

//#region Starlight
[ByRefEvent]
public record struct PaperSignedEvent(EntityUid Signer);
//#endregion Starlight
