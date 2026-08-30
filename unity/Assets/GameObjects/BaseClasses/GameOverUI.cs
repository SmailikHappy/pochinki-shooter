using UnityEngine;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverText;

    private void Awake()
    {
        gameOverText.SetActive(false);
    }

    private void Start()
    {
        GameHandler.instance.onPlayerEliminated += HandlePlayerEliminated;
    }

    private void OnDestroy()
    {
        GameHandler.instance.onPlayerEliminated -= HandlePlayerEliminated;
    }

    private void HandlePlayerEliminated(Player eliminatedPlayer)
    {
        string eliminatedId = eliminatedPlayer?.user?.UniqueId;
        string localId = DiscordHandler.Instance?.LocalUserId;

        bool isLocalPlayer = !string.IsNullOrEmpty(eliminatedId) &&
            string.Equals(eliminatedId, localId, System.StringComparison.Ordinal);

        bool lastPlayerStanding = GameHandler.instance.IsLastPlayerStanding();

        if (!isLocalPlayer && !lastPlayerStanding)
            return; // серверная часть решает исход матча целиком — здесь только личный экран поражения

        gameOverText.SetActive(true);

        gameOverText.GetComponent<TMP_Text>().text = "Game Over";
    }
}