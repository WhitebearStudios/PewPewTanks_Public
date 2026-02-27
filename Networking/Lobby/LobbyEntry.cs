using UnityEngine;
using TMPro;

public class LobbyEntry : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI gameModeText;
    [SerializeField] TextMeshProUGUI playerCountText;

    string lobbyId;

    public void Set(string lobbyName, string gamemode, int playersJoined, int maxPlayers, string lobbyId)
    {
        this.lobbyId = lobbyId;

        nameText.text = lobbyName;
        gameModeText.text = gamemode;
        playerCountText.text = playersJoined + " / " + maxPlayers;
    }

    public void Join()
    {
        PPTLobby.Singleton.JoinLobbyByID(lobbyId);
    }
}
