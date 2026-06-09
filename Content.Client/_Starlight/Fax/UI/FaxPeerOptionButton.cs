using Content.Client._Starlight.UserInterface.Controls;
using Content.Shared.Fax;

namespace Content.Client._Starlight.Fax.UI;

public sealed class FaxPeerOptionButton : ColoredOptionButton
{
    public void AddFaxPeer(KnownFax knownFax)
    {
        AddItem(knownFax.Name);
        SetItemMetadata(ItemCount - 1, knownFax);
        SetItemColor(knownFax.GroupColor ?? Color.Gray);
    }
}
