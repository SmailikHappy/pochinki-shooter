using UnityEngine;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;

    private void OnEnable()
    {
        if (GameHandler.instance != null)
            GameHandler.instance.onPlayerEliminated += HandlePlayerEliminated;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (GameHandler.instance != null)
            GameHandler.instance.onPlayerEliminated -= HandlePlayerEliminated;
    }

    private void HandlePlayerEliminated(Player eliminatedPlayer)
    {
        string eliminatedId = eliminatedPlayer?.user?.UniqueId;
        string localId = DiscordHandler.Instance?.LocalUserId;

        bool isLocalPlayer = !string.IsNullOrEmpty(eliminatedId) &&
            string.Equals(eliminatedId, localId, System.StringComparison.Ordinal);

        if (!isLocalPlayer)
            return; // серверная часть решает исход матча целиком — здесь только личный экран поражения

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = "Game Over";
    }
}