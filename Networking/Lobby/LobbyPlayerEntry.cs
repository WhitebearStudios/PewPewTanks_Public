using UnityEngine;
using UnityEngine.InputSystem;

public class LobbyPlayerEntry : PlayerEntry
{
    [SerializeField] GameObject kickButton;
    RectTransform myRect;
    TMPro.TextMeshProUGUI kickButtonText;

    public bool isHost;
    private bool _isHostsPlayerEntry;
    public bool IsHostsPlayerEntry
    {
        get => _isHostsPlayerEntry;
        set
        {
            _isHostsPlayerEntry = value;
            ChangeRMBtnText();
        }
    }

    void Awake()
    {
        myRect = GetComponent<RectTransform>();
        kickButtonText = kickButton.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>();
    }
    private void Update()
    {
        if (isHost)
        {
            //Show kick button if mouse is hovering over me
            kickButton.SetActive(RectTransformUtility.RectangleContainsScreenPoint(myRect, Mouse.current.position.ReadValue()));
        }
    }
    public override void SetAIMode(int aiMode)
    {
        base.SetAIMode(aiMode);
        ChangeRMBtnText();
    }
    public override void SetAIMode(int aiMode, bool updateDropdown)
    {
        base.SetAIMode(aiMode, updateDropdown);
        ChangeRMBtnText();
    }
    public void ChangeRMBtnText()
    {
        if (tankSettings.aiType == 0 && !IsHostsPlayerEntry)
        {
            kickButtonText.text = "Kick";
            kickButtonText.color = Color.red;
        }
        else
        {
            kickButtonText.text = "Remove";
            kickButtonText.color = Color.black;
        }
    }

    public void UpdateMyTeam(int team)
    {
        PPTLobby.Singleton.UpdatePlayerTeam(team);
    }
}
