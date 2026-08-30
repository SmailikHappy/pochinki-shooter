using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;
    private CanvasGroup canvasGroup;
    private bool eventsBound;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        Hide();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void Start()
    {
        BindEvents();
    }

    private void BindEvents()
    {
        if (eventsBound || GameHandler.instance == null)
            return;

        GameHandler.instance.onPlayerEliminated += HandlePlayerEliminated;
        GameHandler.instance.onMatchEnded += HandleMatchEnded;
        GameHandler.instance.onMatchReset += Hide;
        eventsBound = true;
    }

    private void OnDisable()
    {
        if (eventsBound && GameHandler.instance != null)
        {
            GameHandler.instance.onPlayerEliminated -= HandlePlayerEliminated;
            GameHandler.instance.onMatchEnded -= HandleMatchEnded;
            GameHandler.instance.onMatchReset -= Hide;
        }

        eventsBound = false;
    }

    private void HandlePlayerEliminated(Player eliminatedPlayer)
    {
        string eliminatedId = eliminatedPlayer?.user?.UniqueId;
        string localId = DiscordHandler.Instance?.LocalUserId;

        bool isLocalPlayer = !string.IsNullOrEmpty(eliminatedId) &&
            string.Equals(eliminatedId, localId, System.StringComparison.Ordinal);

        if (!isLocalPlayer)
            return; // серверная часть решает исход матча целиком — здесь только личный экран поражения

        Show("Game Over");
    }

    private void HandleMatchEnded(Player winner)
    {
        string winnerId = winner?.user?.UniqueId;
        string localId = DiscordHandler.Instance?.LocalUserId;
        bool localWon = !string.IsNullOrEmpty(winnerId) &&
            string.Equals(winnerId, localId, System.StringComparison.Ordinal);

        Show(localWon ? "Victory" : "Game Over");
    }

    private void Show(string message)
    {
        if (gameOverText != null)
            gameOverText.text = message;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    private void Hide()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
