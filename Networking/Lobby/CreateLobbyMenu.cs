using UnityEngine;
using UnityEngine.UI;

public class CreateLobbyMenu : MonoBehaviour
{
    [SerializeField] TMPro.TMP_InputField lobbyNameField;
    [SerializeField] Toggle lobbyIsPrivate;
    [SerializeField] TMPro.TMP_Dropdown gamemodeDropdown;
    [SerializeField] Slider maxPlayersSlider;
    [SerializeField] Toggle joinAsHost;

    public void TryCreateLobby()
    {
        if (lobbyNameField.text.Length > 0)
        {
            PPTLobby.Singleton.CreateLobby(lobbyNameField.text, lobbyIsPrivate.isOn, gamemodeDropdown.value, (int)maxPlayersSlider.value, joinAsHost.isOn);
            gameObject.SetActive(false);
        }
    }
}
