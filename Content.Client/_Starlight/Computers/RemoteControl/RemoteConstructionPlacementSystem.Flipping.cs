namespace Content.Client._Starlight.Computers.RemoteControl;

public sealed partial class RemoteConstructionPlacementSystem
{
    public bool TryFlip()
    {
        if (_prototype?.Mirror is not { } mirror)
            return false;

        _prototype = _prototypeManager.Index(mirror);
        _lastPlacementPrototype = null;
        return true;
    }
}
