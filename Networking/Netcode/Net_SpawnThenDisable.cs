using Unity.Netcode;
using UnityEngine;

public class Net_SpawnThenDisable : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        gameObject.SetActive(false);
    }
}
