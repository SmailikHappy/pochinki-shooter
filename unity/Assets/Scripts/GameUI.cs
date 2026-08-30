using UnityEngine;
using UnityEngine.UI;

public sealed class GameUI : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private void Reset()
    {
        if (startButton == null)
        {
            startButton = GetComponentInChildren<Button>();
        }
    }

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }
    }

    public void StartGame()
    {
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        if (GameHandler.instance != null)
        {
            GameHandler.instance.StartGame();
        }
    }
}
