using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class TitleSync : NetworkBehaviour
{
    TextMeshProUGUI text;
    public NetworkVariable<FixedString32Bytes> title = new NetworkVariable<FixedString32Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if(IsServer) title.Value = "Warmup";
        title.OnValueChanged += OnTitleChanged;
        text = GetComponent<TextMeshProUGUI>();
    }

    public override void OnDestroy()
    {
        title.OnValueChanged -= OnTitleChanged;
    }

    public void OnTitleChanged(FixedString32Bytes previous, FixedString32Bytes current)
    {
        text.text = current.ToString();
    }
}
