using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public sealed class NetworkBootstrap : MonoBehaviour
{
    public static NetworkBootstrap Instance { get; private set; }

    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private bool autoStartOnAwake = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!autoStartOnAwake)
        {
            return;
        }

        if (ShouldRunDedicatedServer())
        {
            StartServer();
            return;
        }

        PrepareClient();
    }

    public bool ShouldRunDedicatedServer()
    {
#if UNITY_SERVER
        return true;
#elif UNITY_WEBGL
        return false;
#else
        return Application.platform == RuntimePlatform.WindowsPlayer;
#endif
    }

    public void PrepareClient()
    {
        EnsureNetworkManager();
        ConfigureTransport(isServer: false);
        Debug.Log("Client bootstrap ready. Press Connect to join the local server.");
    }

    public void StartServer()
    {
        EnsureNetworkManager();
        ConfigureTransport(isServer: true);
        NetworkManager.Singleton.StartServer();
    }

    public void ConnectToServer()
    {
        EnsureNetworkManager();
        ConfigureTransport(isServer: false);
        NetworkManager.Singleton.StartClient();
    }

    private void EnsureNetworkManager()
    {
        if (NetworkManager.Singleton == null)
        {
            var networkManagerObject = new GameObject("NetworkManager");
            networkManagerObject.AddComponent<NetworkManager>();
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
        }
    }

    private void ConfigureTransport(bool isServer)
    {
        if (NetworkManager.Singleton == null)
        {
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            return;
        }

        if (isServer)
        {
            transport.SetConnectionData(serverAddress, port, "0.0.0.0");
            return;
        }

        transport.SetConnectionData(serverAddress, port);
    }
}
