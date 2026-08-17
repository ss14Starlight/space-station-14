using Content.Shared._Starlight.Lathe;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._Starlight.Lathe;

public sealed class LatheLinkingUIController : UIController
{
    public void SendToggleMessage(EntityUid uid, bool eject) 
      => EntityManager.RaisePredictiveEvent(new LatheLinkingToggleEvent(EntityManager.GetNetEntity(uid), eject));
}
