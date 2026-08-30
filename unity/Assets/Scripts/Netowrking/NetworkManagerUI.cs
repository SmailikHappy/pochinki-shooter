using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button clientButton;

    private void Awake()
    {
        clientButton.onClick.AddListener(OnClientButtonClicked);
    }

    private void OnClientButtonClicked()
    {
        NetworkManager.Singleton.StartClient();
    }
}
