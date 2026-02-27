using UnityEngine;

public class LobbyMenu : MonoBehaviour
{
    private void OnEnable()
    {
        PPTLobby.Singleton.LobbySelectionMenuActivated();
    }
    private void OnDisable()
    {
        PPTLobby.Singleton.LobbySelectionMenuDeactivated();
    }
}
