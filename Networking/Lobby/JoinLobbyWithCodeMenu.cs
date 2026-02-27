using UnityEngine;

public class JoinLobbyWithCodeMenu : MonoBehaviour
{
    [SerializeField] TMPro.TMP_InputField codeField;
    [SerializeField] GameObject invalidCode;

    public async void TryJoinLobby()
    {
        if (await PPTLobby.Singleton.JoinLobbyByCode(codeField.text))
        {
            gameObject.SetActive(false);
        }
        else invalidCode.SetActive(true);
    }
}
