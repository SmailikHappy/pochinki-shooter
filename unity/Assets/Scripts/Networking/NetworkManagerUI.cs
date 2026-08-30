using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button connectButton;

    private void Awake()
    {
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(ConnectToServer);
        }
    }

    public void ConnectToServer()
    {
        if (NetworkBootstrap.Instance == null)
        {
            Debug.LogWarning("NetworkBootstrap is missing from the scene.");
            return;
        }

        NetworkBootstrap.Instance.ConnectToServer();
    }
}
