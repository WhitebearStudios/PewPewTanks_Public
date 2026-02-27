using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(DamageText))]
public class Net_DamageText : NetworkBehaviour
{
    [ClientRpc]
    public void InitDamageTextClientRpc(string text, float x, float y)
    {
        transform.position = new Vector3(x, y);

        GetComponent<DamageText>().SetText(text);
        GetComponent<RectTransform>().localScale = Vector3.one;
        GetComponent<TMPro.TextMeshProUGUI>().enabled = true; //Show after scaled down
    }
}
