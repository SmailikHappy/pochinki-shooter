using UnityEngine;
using UnityEngine.UI;

public sealed class GameUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button readyButton;

    private Player boundPlayer;

    private void Reset()
    {
        if (startButton == null)
        {
            startButton = GetComponentsInChildren<Button>(true)[0];
        }

        if (readyButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons.Length > 1)
            {
                readyButton = buttons[1];
            }
        }
    }

    private void Awake()
    {
        boundPlayer = GetComponentInParent<Player>() ?? GetComponent<Player>();

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
        }
    }

    private void OnStartClicked()
    {
        if (GameHandler.instance == null)
        {
            return;
        }

        GameHandler.instance.StartGame(force: true);
    }

    private void OnReadyClicked()
    {
        if (boundPlayer == null)
        {
            boundPlayer = GetComponentInParent<Player>() ?? GetComponent<Player>();
        }

        if (boundPlayer == null)
        {
            return;
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }

        if (GameHandler.instance != null)
        {
            GameHandler.instance.MarkPlayerReady(boundPlayer);
        }
        else
        {
            boundPlayer.SetReady(true);
        }
    }

    public void HideLobbyButtons()
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }
    }
}
