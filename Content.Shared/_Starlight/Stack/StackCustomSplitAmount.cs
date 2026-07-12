using Robust.Shared.Serialization;

namespace Content.Shared._Starlight.Stack;

[Serializable, NetSerializable]
public sealed class StackCustomSplitMessage(int value) : BoundUserInterfaceMessage
{
    public int Value = value;
}

[Serializable, NetSerializable]
public enum StackCustomSplitUiKey
{
    Key,
}
