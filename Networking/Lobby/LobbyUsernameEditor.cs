using UnityEngine;
using UnityEngine.UI;

public class LobbyUsernameEditor : MonoBehaviour
{
    [SerializeField] Button done;
    [SerializeField] Button cancel;

    [SerializeField] TMPro.TMP_InputField usernameInput;

    private void OnEnable()
    {
        done.onClick.AddListener(SetUsernameAndClose);
        cancel.onClick.AddListener(Close);

        usernameInput.SetTextWithoutNotify(ProfileSave.username);
    }

    void SetUsernameAndClose()
    {
        ProfileSave.SetUsername(usernameInput.text);

        done.onClick.RemoveListener(SetUsernameAndClose);
        cancel.onClick.RemoveListener(Close);
        gameObject.SetActive(false);
    }
    void Close()
    {
        done.onClick.RemoveListener(SetUsernameAndClose);
        cancel.onClick.RemoveListener(Close);
        gameObject.SetActive(false);
    }
}
