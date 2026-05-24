using System.Numerics;
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
    /// Drawing strokes saved on this paper.
    /// These are rendered below the written text by the client UI.
    /// </summary>
    [DataField("drawing"), AutoNetworkedField]
    public List<PaperDrawingStroke> Drawing { get; set; } = new();

    /// <summary>
    /// Maximum amount of strokes allowed on one paper.
    /// </summary>
    [DataField("maxDrawingStrokes")]
    public int MaxDrawingStrokes = 128;

    /// <summary>
    /// Maximum amount of points allowed per stroke.
    /// </summary>
    [DataField("maxDrawingPointsPerStroke")]
    public int MaxDrawingPointsPerStroke = 128;

    /// <summary>
    /// Maximum total amount of drawing points allowed on one paper.
    /// </summary>
    [DataField("maxDrawingPoints")]
    public int MaxDrawingPoints = 4096;

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperDrawingStroke
    {
        public readonly List<Vector2> Points;
        public readonly float Thickness;

        public PaperDrawingStroke(List<Vector2> points, float thickness = 2f)
        {
            Points = points;
            Thickness = thickness;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly PaperAction Mode;
        public readonly List<PaperDrawingStroke> Drawing;

        public PaperBoundUserInterfaceState(
            string text,
            List<StampDisplayInfo> stampedBy,
            List<PaperDrawingStroke> drawing,
            PaperAction mode = PaperAction.Read)
        {
            Text = text;
            StampedBy = stampedBy;
            Drawing = drawing;
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
        public readonly List<PaperDrawingStroke> Drawing;

        public PaperInputDrawingMessage(List<PaperDrawingStroke> drawing)
        {
            Drawing = drawing;
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
