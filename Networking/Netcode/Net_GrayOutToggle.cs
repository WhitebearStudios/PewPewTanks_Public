using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Net_GrayOutToggle : NetworkBehaviour
{
    public bool Enabled;

    private void Awake()
    {
        GetComponent<Toggle>().onValueChanged.AddListener(SendToggleChangeIfMultiplayer);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        gameObject.SetActive(Enabled);
    }

    void SendToggleChangeIfMultiplayer(bool isOn)
    {
        if (GameManager.MultiplayerGame) UpdateToggleClientRpc(isOn);
    }
    [ClientRpc]
    void UpdateToggleClientRpc(bool isOn)
    {
        if(!IsHost) GetComponent<GrayOutToggle>().Toggle(isOn);
    }
}
